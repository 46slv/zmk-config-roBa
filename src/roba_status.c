/*
 * Copyright (c) 2026 roBa contributors
 *
 * SPDX-License-Identifier: MIT
 */

#include <errno.h>
#include <string.h>

#include <zephyr/bluetooth/gatt.h>
#include <zephyr/logging/log.h>
#include <zephyr/sys/byteorder.h>
#include <zephyr/sys/util.h>

#include <zmk/battery.h>
#include <zmk/event_manager.h>
#include <zmk/events/battery_state_changed.h>
#include <zmk/events/layer_state_changed.h>
#include <zmk/keymap.h>
#include <zmk/split/central.h>

LOG_MODULE_REGISTER(roba_status, CONFIG_ZMK_LOG_LEVEL);

#define ROBA_STATUS_PROTOCOL_VERSION 1
#define ROBA_STATUS_UNKNOWN_BATTERY UINT8_MAX

#define ROBA_STATUS_SERVICE_UUID                                                                 \
    BT_UUID_128_ENCODE(0x5a0e1000, 0x7c7f, 0x4b52, 0xa8a8, 0x3f5c726f4261)
#define ROBA_STATUS_CHARACTERISTIC_UUID                                                          \
    BT_UUID_128_ENCODE(0x5a0e1001, 0x7c7f, 0x4b52, 0xa8a8, 0x3f5c726f4261)

static struct bt_uuid_128 roba_status_service_uuid =
    BT_UUID_INIT_128(ROBA_STATUS_SERVICE_UUID);
static struct bt_uuid_128 roba_status_characteristic_uuid =
    BT_UUID_INIT_128(ROBA_STATUS_CHARACTERISTIC_UUID);

struct roba_status_packet {
    uint8_t version;
    uint8_t message_flags;
    uint8_t highest_layer;
    uint32_t active_layer_mask_le;
    uint8_t right_battery;
    uint8_t left_battery;
    uint8_t status_flags;
    uint16_t sequence_le;
} __packed;

BUILD_ASSERT(sizeof(struct roba_status_packet) == 12, "roBa status packet size changed");

static uint16_t sequence;
static bool notifications_enabled;
static uint8_t right_battery = ROBA_STATUS_UNKNOWN_BATTERY;
static uint8_t left_battery = ROBA_STATUS_UNKNOWN_BATTERY;

static void populate_packet(struct roba_status_packet *packet) {
    packet->version = ROBA_STATUS_PROTOCOL_VERSION;
    packet->message_flags = 0;
    packet->highest_layer = zmk_keymap_highest_layer_active();
    packet->active_layer_mask_le = sys_cpu_to_le32(zmk_keymap_layer_state());
    packet->right_battery = right_battery;
    packet->left_battery = left_battery;
    packet->status_flags = 0;
    packet->sequence_le = sys_cpu_to_le16(sequence);
}

static ssize_t read_status(struct bt_conn *conn, const struct bt_gatt_attr *attr, void *buf,
                           uint16_t len, uint16_t offset) {
    struct roba_status_packet packet;
    populate_packet(&packet);
    return bt_gatt_attr_read(conn, attr, buf, len, offset, &packet, sizeof(packet));
}

static void status_ccc_changed(const struct bt_gatt_attr *attr, uint16_t value) {
    ARG_UNUSED(attr);
    notifications_enabled = (value == BT_GATT_CCC_NOTIFY);
    LOG_INF("roBa status notifications %s", notifications_enabled ? "enabled" : "disabled");
}

BT_GATT_SERVICE_DEFINE(
    roba_status_service,
    BT_GATT_PRIMARY_SERVICE(&roba_status_service_uuid),
    BT_GATT_CHARACTERISTIC(&roba_status_characteristic_uuid.uuid,
                           BT_GATT_CHRC_READ | BT_GATT_CHRC_NOTIFY,
                           BT_GATT_PERM_READ_ENCRYPT,
                           read_status,
                           NULL,
                           NULL),
    BT_GATT_CCC(status_ccc_changed, BT_GATT_PERM_READ_ENCRYPT | BT_GATT_PERM_WRITE_ENCRYPT));

static void notify_status(void) {
    if (!notifications_enabled) {
        return;
    }

    struct roba_status_packet packet;
    sequence++;
    populate_packet(&packet);

    int ret = bt_gatt_notify(NULL, &roba_status_service.attrs[1], &packet, sizeof(packet));
    if (ret < 0 && ret != -ENOTCONN) {
        LOG_WRN("Failed to notify roBa status: %d", ret);
    }
}

static int roba_status_listener(const zmk_event_t *eh) {
    const struct zmk_battery_state_changed *battery_ev = as_zmk_battery_state_changed(eh);
    if (battery_ev != NULL) {
        right_battery = battery_ev->state_of_charge;
        notify_status();
        return ZMK_EV_EVENT_BUBBLE;
    }

    const struct zmk_peripheral_battery_state_changed *peripheral_ev =
        as_zmk_peripheral_battery_state_changed(eh);
    if (peripheral_ev != NULL) {
        if (peripheral_ev->source == 0) {
            left_battery = peripheral_ev->state_of_charge;
            notify_status();
        }
        return ZMK_EV_EVENT_BUBBLE;
    }

    if (as_zmk_layer_state_changed(eh) != NULL) {
        notify_status();
    }

    return ZMK_EV_EVENT_BUBBLE;
}

static int roba_status_init(void) {
    right_battery = zmk_battery_state_of_charge();
    uint8_t peripheral_level;
    if (zmk_split_central_get_peripheral_battery_level(0, &peripheral_level) == 0) {
        left_battery = peripheral_level;
    }
    return 0;
}

SYS_INIT(roba_status_init, APPLICATION, CONFIG_APPLICATION_INIT_PRIORITY);

ZMK_LISTENER(roba_status, roba_status_listener);
ZMK_SUBSCRIPTION(roba_status, zmk_layer_state_changed);
ZMK_SUBSCRIPTION(roba_status, zmk_battery_state_changed);
ZMK_SUBSCRIPTION(roba_status, zmk_peripheral_battery_state_changed);
