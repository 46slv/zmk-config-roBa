/*
 * Copyright (c) 2026 roBa contributors
 *
 * SPDX-License-Identifier: MIT
 */

#define DT_DRV_COMPAT zmk_behavior_roba_encoder_scroll

#include <errno.h>
#include <limits.h>
#include <stdbool.h>
#include <stdint.h>

#include <zephyr/device.h>
#include <zephyr/drivers/sensor.h>
#include <zephyr/kernel.h>
#include <zephyr/logging/log.h>
#include <zephyr/sys/util.h>

#include <drivers/behavior.h>

#include <zmk/behavior.h>
#include <zmk/keymap.h>
#include <zmk/sensors.h>
#include <zmk/virtual_key_position.h>

#if !IS_ENABLED(CONFIG_ZMK_SPLIT) || IS_ENABLED(CONFIG_ZMK_SPLIT_ROLE_CENTRAL)
#include <zmk/endpoints.h>
#include <zmk/event_manager.h>
#include <zmk/events/endpoint_changed.h>
#include <zmk/events/layer_state_changed.h>
#include <zmk/hid.h>
#endif

#include "roba_encoder_scroll_math.h"

LOG_MODULE_REGISTER(roba_encoder_scroll, CONFIG_ZMK_LOG_LEVEL);

enum roba_encoder_scroll_runtime_state {
    ROBA_ENCODER_SCROLL_IDLE,
    ROBA_ENCODER_SCROLL_TRACKING,
    ROBA_ENCODER_SCROLL_COASTING,
};

struct behavior_roba_encoder_scroll_config {
    struct roba_encoder_scroll_config acceleration;
    int16_t base_delta;
    uint16_t stop_ms;
    uint16_t coast_tick_ms;
    uint8_t coast_start_multiplier;
    uint8_t coast_ticks;
    bool inertia_enabled;
};

struct behavior_roba_encoder_scroll_data {
    const struct device *dev;
    struct sensor_value remainder[ZMK_KEYMAP_SENSORS_LEN][ZMK_KEYMAP_LAYERS_LEN];
    int triggers[ZMK_KEYMAP_SENSORS_LEN][ZMK_KEYMAP_LAYERS_LEN];
    int64_t accepted_at[ZMK_KEYMAP_SENSORS_LEN][ZMK_KEYMAP_LAYERS_LEN];
    struct roba_encoder_scroll_state acceleration;
    struct k_work_delayable stop_work;
    struct k_work_delayable coast_work;
    enum roba_encoder_scroll_runtime_state runtime_state;
    int8_t coast_direction;
    uint8_t coast_index;
};

static void clear_runtime_state(struct behavior_roba_encoder_scroll_data *data) {
    data->runtime_state = ROBA_ENCODER_SCROLL_IDLE;
    data->coast_direction = 0;
    data->coast_index = 0;
    roba_encoder_scroll_state_reset(&data->acceleration);
}

static void cancel_pending_work(struct behavior_roba_encoder_scroll_data *data) {
    k_work_cancel_delayable(&data->stop_work);
    k_work_cancel_delayable(&data->coast_work);
}

#if !IS_ENABLED(CONFIG_ZMK_SPLIT) || IS_ENABLED(CONFIG_ZMK_SPLIT_ROLE_CENTRAL)
static void reset_all_state(struct behavior_roba_encoder_scroll_data *data) {
    cancel_pending_work(data);
    clear_runtime_state(data);
}
#endif

static int emit_scroll(const struct behavior_roba_encoder_scroll_config *config,
                       int8_t direction, uint8_t multiplier) {
#if !IS_ENABLED(CONFIG_ZMK_SPLIT) || IS_ENABLED(CONFIG_ZMK_SPLIT_ROLE_CENTRAL)
    int32_t requested = (int32_t)config->base_delta * direction * multiplier;
    int16_t delta = (int16_t)CLAMP(requested, INT16_MIN, INT16_MAX);

    zmk_hid_mouse_scroll_set(0, delta);
    int ret = zmk_endpoints_send_mouse_report();
    zmk_hid_mouse_scroll_set(0, 0);
    return ret;
#else
    ARG_UNUSED(config);
    ARG_UNUSED(direction);
    ARG_UNUSED(multiplier);
    return -ENOTSUP;
#endif
}

static void coast_handler(struct k_work *work) {
    struct k_work_delayable *dwork = k_work_delayable_from_work(work);
    struct behavior_roba_encoder_scroll_data *data =
        CONTAINER_OF(dwork, struct behavior_roba_encoder_scroll_data, coast_work);
    const struct behavior_roba_encoder_scroll_config *config = data->dev->config;

    if (data->runtime_state != ROBA_ENCODER_SCROLL_COASTING) {
        return;
    }

    uint8_t multiplier = roba_encoder_scroll_tail_multiplier(
        config->coast_start_multiplier, config->coast_ticks, data->coast_index);
    if (multiplier == 0) {
        clear_runtime_state(data);
        return;
    }

    int ret = emit_scroll(config, data->coast_direction, multiplier);
    if (ret < 0 && ret != -ENOTSUP) {
        LOG_WRN("Failed to emit coast report: %d", ret);
    }

    data->coast_index++;
    if (data->coast_index >= config->coast_ticks) {
        clear_runtime_state(data);
        return;
    }

    k_work_schedule(&data->coast_work, K_MSEC(config->coast_tick_ms));
}

static void stop_handler(struct k_work *work) {
    struct k_work_delayable *dwork = k_work_delayable_from_work(work);
    struct behavior_roba_encoder_scroll_data *data =
        CONTAINER_OF(dwork, struct behavior_roba_encoder_scroll_data, stop_work);
    const struct behavior_roba_encoder_scroll_config *config = data->dev->config;

    if (data->runtime_state != ROBA_ENCODER_SCROLL_TRACKING ||
        !config->inertia_enabled || !data->acceleration.inertia_armed) {
        return;
    }

    data->coast_direction = data->acceleration.direction;
    data->coast_index = 0;
    data->runtime_state = ROBA_ENCODER_SCROLL_COASTING;
    roba_encoder_scroll_state_reset(&data->acceleration);
    k_work_schedule(&data->coast_work, K_NO_WAIT);
}

static int accept_sensor_data(struct zmk_behavior_binding *binding,
                              struct zmk_behavior_binding_event event,
                              const struct zmk_sensor_config *sensor_config,
                              size_t channel_data_size,
                              const struct zmk_sensor_channel_data *channel_data) {
    const struct device *dev = zmk_behavior_get_binding(binding->behavior_dev);
    struct behavior_roba_encoder_scroll_data *data = dev->data;
    int sensor_index = ZMK_SENSOR_POSITION_FROM_VIRTUAL_KEY_POSITION(event.position);

    if (channel_data_size == 0 || sensor_index < 0 ||
        sensor_index >= ZMK_KEYMAP_SENSORS_LEN || event.layer >= ZMK_KEYMAP_LAYERS_LEN) {
        return -EINVAL;
    }

    const struct sensor_value value = channel_data[0].value;
    int triggers;

    if (value.val1 == 0) {
        triggers = value.val2;
    } else {
        if (sensor_config == NULL || sensor_config->triggers_per_rotation <= 0) {
            return -EINVAL;
        }

        struct sensor_value *remainder = &data->remainder[sensor_index][event.layer];
        remainder->val1 += value.val1;
        remainder->val2 += value.val2;

        if (remainder->val2 >= 1000000 || remainder->val2 <= -1000000) {
            remainder->val1 += remainder->val2 / 1000000;
            remainder->val2 %= 1000000;
        }

        int trigger_degrees = MAX(1, 360 / sensor_config->triggers_per_rotation);
        triggers = remainder->val1 / trigger_degrees;
        remainder->val1 %= trigger_degrees;
    }

    data->triggers[sensor_index][event.layer] = triggers;
    data->accepted_at[sensor_index][event.layer] =
        event.timestamp > 0 ? event.timestamp : k_uptime_get();
    return 0;
}

static int process_sensor_data(struct zmk_behavior_binding *binding,
                               struct zmk_behavior_binding_event event,
                               enum behavior_sensor_binding_process_mode mode) {
    const struct device *dev = zmk_behavior_get_binding(binding->behavior_dev);
    const struct behavior_roba_encoder_scroll_config *config = dev->config;
    struct behavior_roba_encoder_scroll_data *data = dev->data;
    int sensor_index = ZMK_SENSOR_POSITION_FROM_VIRTUAL_KEY_POSITION(event.position);

    if (sensor_index < 0 || sensor_index >= ZMK_KEYMAP_SENSORS_LEN ||
        event.layer >= ZMK_KEYMAP_LAYERS_LEN) {
        return -EINVAL;
    }

    if (mode != BEHAVIOR_SENSOR_BINDING_PROCESS_MODE_TRIGGER) {
        data->triggers[sensor_index][event.layer] = 0;
        return ZMK_BEHAVIOR_TRANSPARENT;
    }

    int signed_triggers = data->triggers[sensor_index][event.layer];
    data->triggers[sensor_index][event.layer] = 0;
    if (signed_triggers == 0) {
        return ZMK_BEHAVIOR_TRANSPARENT;
    }

    int8_t direction = signed_triggers > 0 ? 1 : -1;
    int trigger_count = signed_triggers > 0 ? signed_triggers : -signed_triggers;
    int64_t accepted_at = data->accepted_at[sensor_index][event.layer];

    cancel_pending_work(data);
    if (data->runtime_state == ROBA_ENCODER_SCROLL_COASTING) {
        clear_runtime_state(data);
    }
    data->runtime_state = ROBA_ENCODER_SCROLL_TRACKING;

    struct roba_encoder_scroll_result result = roba_encoder_scroll_feed(
        &data->acceleration, &config->acceleration, direction, accepted_at);

    for (int i = 0; i < trigger_count; i++) {
        int ret = emit_scroll(config, direction, result.multiplier);
        if (ret < 0 && ret != -ENOTSUP) {
            LOG_WRN("Failed to emit encoder scroll report: %d", ret);
        }
    }

    if (config->inertia_enabled && data->acceleration.inertia_armed) {
        k_work_schedule(&data->stop_work, K_MSEC(config->stop_ms));
    }

    return ZMK_BEHAVIOR_OPAQUE;
}

static int behavior_roba_encoder_scroll_init(const struct device *dev) {
    struct behavior_roba_encoder_scroll_data *data = dev->data;
    const struct behavior_roba_encoder_scroll_config *config = dev->config;

    if (config->acceleration.six_x_ms > config->acceleration.four_x_ms ||
        config->acceleration.four_x_ms > config->acceleration.two_x_ms ||
        config->acceleration.two_x_ms >= config->acceleration.reset_ms ||
        config->acceleration.fast_streak == 0 || config->acceleration.inertia_streak == 0 ||
        config->base_delta <= 0 || config->coast_tick_ms == 0 ||
        config->coast_start_multiplier == 0 || config->coast_ticks == 0 ||
        config->coast_ticks > config->coast_start_multiplier) {
        LOG_ERR("Invalid encoder scroll configuration");
        return -EINVAL;
    }

    data->dev = dev;
    clear_runtime_state(data);
    k_work_init_delayable(&data->stop_work, stop_handler);
    k_work_init_delayable(&data->coast_work, coast_handler);
    return 0;
}

static const struct behavior_driver_api behavior_roba_encoder_scroll_driver_api = {
    .sensor_binding_accept_data = accept_sensor_data,
    .sensor_binding_process = process_sensor_data,
};

#define ROBA_ENCODER_SCROLL_INST(n)                                                               \
    static struct behavior_roba_encoder_scroll_data behavior_roba_encoder_scroll_data_##n = {};  \
    static const struct behavior_roba_encoder_scroll_config                                       \
        behavior_roba_encoder_scroll_config_##n = {                                               \
            .acceleration =                                                                       \
                {                                                                                 \
                    .two_x_ms = DT_INST_PROP(n, two_x_ms),                                        \
                    .four_x_ms = DT_INST_PROP(n, four_x_ms),                                      \
                    .six_x_ms = DT_INST_PROP(n, six_x_ms),                                        \
                    .reset_ms = DT_INST_PROP(n, reset_ms),                                        \
                    .fast_streak = DT_INST_PROP(n, fast_streak),                                  \
                    .inertia_streak = DT_INST_PROP(n, inertia_streak),                            \
                },                                                                                \
            .base_delta = DT_INST_PROP(n, base_delta),                                            \
            .stop_ms = DT_INST_PROP(n, stop_ms),                                                  \
            .coast_tick_ms = DT_INST_PROP(n, coast_tick_ms),                                      \
            .coast_start_multiplier = DT_INST_PROP(n, coast_start_multiplier),                    \
            .coast_ticks = DT_INST_PROP(n, coast_ticks),                                          \
            .inertia_enabled = DT_INST_PROP(n, inertia_enabled),                                  \
    };                                                                                            \
    BEHAVIOR_DT_INST_DEFINE(n, behavior_roba_encoder_scroll_init, NULL,                            \
                            &behavior_roba_encoder_scroll_data_##n,                                \
                            &behavior_roba_encoder_scroll_config_##n, POST_KERNEL,                 \
                            CONFIG_KERNEL_INIT_PRIORITY_DEFAULT,                                  \
                            &behavior_roba_encoder_scroll_driver_api);

DT_INST_FOREACH_STATUS_OKAY(ROBA_ENCODER_SCROLL_INST)

#if !IS_ENABLED(CONFIG_ZMK_SPLIT) || IS_ENABLED(CONFIG_ZMK_SPLIT_ROLE_CENTRAL)
#define ROBA_ENCODER_SCROLL_RESET_INSTANCE(n) reset_all_state(&behavior_roba_encoder_scroll_data_##n);

static int behavior_roba_encoder_scroll_event_listener(const zmk_event_t *eh) {
    if (as_zmk_layer_state_changed(eh) != NULL || as_zmk_endpoint_changed(eh) != NULL) {
        DT_INST_FOREACH_STATUS_OKAY(ROBA_ENCODER_SCROLL_RESET_INSTANCE)
    }
    return ZMK_EV_EVENT_BUBBLE;
}

ZMK_LISTENER(roba_encoder_scroll, behavior_roba_encoder_scroll_event_listener);
ZMK_SUBSCRIPTION(roba_encoder_scroll, zmk_layer_state_changed);
ZMK_SUBSCRIPTION(roba_encoder_scroll, zmk_endpoint_changed);
#endif
