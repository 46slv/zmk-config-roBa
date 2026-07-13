/*
 * Copyright (c) 2026 roBa contributors
 *
 * SPDX-License-Identifier: MIT
 */

#pragma once

#include <stdbool.h>
#include <stdint.h>

struct roba_encoder_scroll_config {
    uint16_t two_x_ms;
    uint16_t four_x_ms;
    uint16_t six_x_ms;
    uint16_t reset_ms;
    uint8_t fast_streak;
    uint8_t inertia_streak;
};

struct roba_encoder_scroll_state {
    int64_t last_time_ms;
    int8_t direction;
    uint8_t quick_streak;
    uint8_t max_streak;
    bool inertia_armed;
};

struct roba_encoder_scroll_result {
    uint8_t multiplier;
    bool inertia_armed;
};

static inline void
roba_encoder_scroll_state_reset(struct roba_encoder_scroll_state *state) {
    state->last_time_ms = 0;
    state->direction = 0;
    state->quick_streak = 0;
    state->max_streak = 0;
    state->inertia_armed = false;
}

static inline uint8_t
roba_encoder_scroll_requested_multiplier(const struct roba_encoder_scroll_config *config,
                                         int64_t elapsed_ms) {
    if (elapsed_ms < config->six_x_ms) {
        return 6;
    }
    if (elapsed_ms < config->four_x_ms) {
        return 4;
    }
    if (elapsed_ms < config->two_x_ms) {
        return 2;
    }
    return 1;
}

static inline struct roba_encoder_scroll_result
roba_encoder_scroll_feed(struct roba_encoder_scroll_state *state,
                         const struct roba_encoder_scroll_config *config, int8_t direction,
                         int64_t now_ms) {
    struct roba_encoder_scroll_result result = {.multiplier = 1, .inertia_armed = false};

    bool first = state->last_time_ms == 0;
    int64_t elapsed_ms = first ? 0 : now_ms - state->last_time_ms;
    bool reset = first || direction != state->direction || elapsed_ms < 0 ||
                 elapsed_ms >= config->reset_ms;

    if (reset) {
        state->quick_streak = 0;
        state->max_streak = 0;
        state->inertia_armed = false;
    } else {
        uint8_t requested = roba_encoder_scroll_requested_multiplier(config, elapsed_ms);

        if (requested >= 4) {
            if (state->quick_streak < UINT8_MAX) {
                state->quick_streak++;
            }

            uint16_t two_x_end = config->fast_streak;
            uint16_t four_x_end = two_x_end * 2U;
            uint8_t ramp_limit = state->quick_streak <= two_x_end    ? 2
                                 : state->quick_streak <= four_x_end ? 4
                                                                    : 6;
            result.multiplier = requested < ramp_limit ? requested : ramp_limit;
        } else {
            state->quick_streak = 0;
            result.multiplier = requested;
        }

        if (result.multiplier == 6) {
            if (state->max_streak < UINT8_MAX) {
                state->max_streak++;
            }
            if (state->max_streak >= config->inertia_streak) {
                state->inertia_armed = true;
            }
        } else {
            state->max_streak = 0;
            state->inertia_armed = false;
        }
    }

    state->last_time_ms = now_ms;
    state->direction = direction;
    result.inertia_armed = state->inertia_armed;
    return result;
}

static inline uint8_t roba_encoder_scroll_tail_multiplier(uint8_t start_multiplier,
                                                          uint8_t tick_count,
                                                          uint8_t tick_index) {
    if (tick_index >= tick_count || tick_index >= start_multiplier) {
        return 0;
    }
    return start_multiplier - tick_index;
}
