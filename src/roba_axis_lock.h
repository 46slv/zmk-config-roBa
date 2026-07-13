/* Axis-selection state for roBa's unified scroll processor.
 * Zephyr-free so retention and lock rules can be host-tested.
 * SPDX-License-Identifier: MIT
 */
#ifndef ROBA_AXIS_LOCK_H
#define ROBA_AXIS_LOCK_H

#include <stdbool.h>
#include <stdint.h>
#include <limits.h>

enum roba_axis_direction { ROBA_AXIS_NONE = 0, ROBA_AXIS_X = 1, ROBA_AXIS_Y = 2 };
enum roba_axis_action { ROBA_AXIS_WAIT = 0, ROBA_AXIS_OUTPUT = 1 };

struct roba_axis_lock_config {
    int32_t x_ratio_num, x_ratio_den;
    int32_t y_ratio_num, y_ratio_den;
    int32_t require_events;
    int32_t immediate_threshold;
    int32_t lock_ms;
    int32_t lock_events;
    int32_t idle_reset_ms;
    int32_t switch_threshold;
    int32_t mode; /* 0 = snap, 1 = free */
};

struct roba_axis_lock_state {
    enum roba_axis_direction direction;
    int32_t pending_x, pending_y;
    int32_t abs_x, abs_y;
    int32_t event_count;
    int32_t lock_events_remaining;
    int64_t lock_expires_at_ms;
    int64_t last_event_ms;
    bool has_event;
};

struct roba_axis_lock_result {
    enum roba_axis_action action;
    enum roba_axis_direction direction;
    int32_t value;
    bool direction_changed;
};

static inline int32_t roba_abs_sat(int32_t value) {
    return value == INT32_MIN ? INT32_MAX : (value < 0 ? -value : value);
}

static inline int32_t roba_add_sat(int32_t a, int32_t b) {
    int64_t sum = (int64_t)a + b;
    return sum > INT32_MAX ? INT32_MAX : sum < INT32_MIN ? INT32_MIN : (int32_t)sum;
}

static inline void roba_axis_lock_reset(struct roba_axis_lock_state *state) {
    state->direction = ROBA_AXIS_NONE;
    state->pending_x = state->pending_y = 0;
    state->abs_x = state->abs_y = 0;
    state->event_count = 0;
    state->lock_events_remaining = 0;
    state->lock_expires_at_ms = 0;
    state->last_event_ms = 0;
    state->has_event = false;
}

static inline bool roba_axis_lock_active(const struct roba_axis_lock_state *state,
                                         const struct roba_axis_lock_config *config,
                                         int64_t now_ms) {
    bool time_active = config->lock_ms > 0 && now_ms < state->lock_expires_at_ms;
    return time_active || state->lock_events_remaining > 0;
}

static inline enum roba_axis_direction roba_axis_choose(
    const struct roba_axis_lock_state *state,
    const struct roba_axis_lock_config *config) {
    int64_t ax = state->abs_x, ay = state->abs_y;
    if (ay * config->y_ratio_den > ax * config->y_ratio_num) return ROBA_AXIS_Y;
    if (ay * config->x_ratio_den < ax * config->x_ratio_num) return ROBA_AXIS_X;
    /* Flush the retained gesture even in the old module's undecided band. */
    return ay >= ax ? ROBA_AXIS_Y : ROBA_AXIS_X;
}

static inline void roba_axis_start_lock(struct roba_axis_lock_state *state,
                                        const struct roba_axis_lock_config *config,
                                        enum roba_axis_direction direction,
                                        int64_t now_ms) {
    state->direction = direction;
    state->lock_events_remaining = config->lock_events;
    state->lock_expires_at_ms = config->lock_ms > 0 ? now_ms + config->lock_ms : 0;
}

static inline void roba_axis_accumulate(struct roba_axis_lock_state *state,
                                        enum roba_axis_direction axis,
                                        int32_t value) {
    if (axis == ROBA_AXIS_X) {
        state->pending_x = roba_add_sat(state->pending_x, value);
        state->abs_x = roba_add_sat(state->abs_x, roba_abs_sat(value));
    } else {
        state->pending_y = roba_add_sat(state->pending_y, value);
        state->abs_y = roba_add_sat(state->abs_y, roba_abs_sat(value));
    }
    state->event_count++;
}

static inline void roba_axis_clear_pending(struct roba_axis_lock_state *state) {
    state->pending_x = state->pending_y = 0;
    state->abs_x = state->abs_y = 0;
    state->event_count = 0;
}

static inline struct roba_axis_lock_result roba_axis_lock_feed(
    struct roba_axis_lock_state *state,
    const struct roba_axis_lock_config *config,
    enum roba_axis_direction event_axis,
    int32_t value,
    int64_t now_ms) {
    struct roba_axis_lock_result result = {ROBA_AXIS_WAIT, state->direction, 0, false};

    if (config->mode == 1) {
        result.action = ROBA_AXIS_OUTPUT;
        result.direction = event_axis;
        result.value = value;
        result.direction_changed = state->direction != event_axis;
        state->direction = event_axis;
        state->last_event_ms = now_ms;
        state->has_event = true;
        return result;
    }

    if (state->has_event && config->idle_reset_ms > 0 &&
        now_ms - state->last_event_ms >= config->idle_reset_ms) {
        roba_axis_lock_reset(state);
    }
    state->last_event_ms = now_ms;
    state->has_event = true;

    if (state->direction != ROBA_AXIS_NONE) {
        if (state->lock_events_remaining > 0) state->lock_events_remaining--;
        if (event_axis == state->direction) {
            roba_axis_clear_pending(state);
            if (config->lock_ms > 0) state->lock_expires_at_ms = now_ms + config->lock_ms;
            result.action = ROBA_AXIS_OUTPUT;
            result.direction = state->direction;
            result.value = value;
            return result;
        }

        roba_axis_accumulate(state, event_axis, value);
        if (roba_axis_lock_active(state, config, now_ms)) return result;
        int32_t cross_abs = event_axis == ROBA_AXIS_X ? state->abs_x : state->abs_y;
        if (cross_abs < config->switch_threshold) return result;

        enum roba_axis_direction old = state->direction;
        int32_t output = event_axis == ROBA_AXIS_X ? state->pending_x : state->pending_y;
        roba_axis_clear_pending(state);
        roba_axis_start_lock(state, config, event_axis, now_ms);
        result.action = ROBA_AXIS_OUTPUT;
        result.direction = event_axis;
        result.value = output;
        result.direction_changed = old != event_axis;
        return result;
    }

    roba_axis_accumulate(state, event_axis, value);
    int32_t required = config->require_events > 0 ? config->require_events : 1;
    bool enough = state->event_count >= required;
    bool immediate = state->abs_x >= config->immediate_threshold ||
                     state->abs_y >= config->immediate_threshold;
    if (!enough && !immediate) return result;

    enum roba_axis_direction selected = roba_axis_choose(state, config);
    int32_t output = selected == ROBA_AXIS_X ? state->pending_x : state->pending_y;
    roba_axis_clear_pending(state);
    roba_axis_start_lock(state, config, selected, now_ms);
    result.action = ROBA_AXIS_OUTPUT;
    result.direction = selected;
    result.value = output;
    result.direction_changed = true;
    return result;
}

#endif
