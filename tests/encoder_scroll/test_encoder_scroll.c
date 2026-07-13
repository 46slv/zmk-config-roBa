/* SPDX-License-Identifier: MIT */

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#include "roba_encoder_scroll_math.h"

static const struct roba_encoder_scroll_config config = {
    .two_x_ms = 240,
    .four_x_ms = 140,
    .six_x_ms = 80,
    .reset_ms = 320,
    .fast_streak = 1,
    .inertia_streak = 2,
};

static struct roba_encoder_scroll_state fresh_state(void) {
    struct roba_encoder_scroll_state state;
    roba_encoder_scroll_state_reset(&state);
    return state;
}

static void test_threshold_boundaries(void) {
    assert(roba_encoder_scroll_requested_multiplier(&config, 79) == 6);
    assert(roba_encoder_scroll_requested_multiplier(&config, 80) == 4);
    assert(roba_encoder_scroll_requested_multiplier(&config, 139) == 4);
    assert(roba_encoder_scroll_requested_multiplier(&config, 140) == 2);
    assert(roba_encoder_scroll_requested_multiplier(&config, 239) == 2);
    assert(roba_encoder_scroll_requested_multiplier(&config, 240) == 1);
}

static void test_first_and_slow_detents_stay_precise(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1000).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1250).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1500).multiplier == 1);
    assert(!state.inertia_armed);
}

static void test_fast_guard_and_max_arm(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1060, 1120, 1180, 1240};
    const uint8_t expected[] = {1, 2, 4, 6, 6};

    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        struct roba_encoder_scroll_result result =
            roba_encoder_scroll_feed(&state, &config, 1, times[i]);
        assert(result.multiplier == expected[i]);
        assert(result.inertia_armed == (i == 4));
    }
}

static void test_four_x_ramps_after_two_matching_intervals(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1000).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1120).multiplier == 2);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1240).multiplier == 4);
}

static void test_fast_streak_controls_each_ramp_stage(void) {
    const struct roba_encoder_scroll_config guarded_config = {
        .two_x_ms = 240,
        .four_x_ms = 140,
        .six_x_ms = 80,
        .reset_ms = 320,
        .fast_streak = 2,
        .inertia_streak = 2,
    };
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1060, 1120, 1180, 1240, 1300};
    const uint8_t expected[] = {1, 2, 2, 4, 4, 6};

    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        struct roba_encoder_scroll_result result =
            roba_encoder_scroll_feed(&state, &guarded_config, 1, times[i]);
        assert(result.multiplier == expected[i]);
    }
}

static void test_direction_change_resets_to_one(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1060, 1120};
    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        roba_encoder_scroll_feed(&state, &config, 1, times[i]);
    }

    struct roba_encoder_scroll_result reversed =
        roba_encoder_scroll_feed(&state, &config, -1, 1180);
    assert(reversed.multiplier == 1);
    assert(!reversed.inertia_armed);
}

static void test_idle_gap_resets_to_one(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    roba_encoder_scroll_feed(&state, &config, 1, 1000);
    roba_encoder_scroll_feed(&state, &config, 1, 1060);
    roba_encoder_scroll_feed(&state, &config, 1, 1120);

    struct roba_encoder_scroll_result after_gap =
        roba_encoder_scroll_feed(&state, &config, 1, 1440);
    assert(after_gap.multiplier == 1);
    assert(!after_gap.inertia_armed);
}

static void test_medium_speed_disarms_inertia(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1060, 1120, 1180, 1240};
    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        roba_encoder_scroll_feed(&state, &config, 1, times[i]);
    }
    assert(state.inertia_armed);

    struct roba_encoder_scroll_result medium =
        roba_encoder_scroll_feed(&state, &config, 1, 1400);
    assert(medium.multiplier == 2);
    assert(!medium.inertia_armed);
}

static void test_tail_is_finite(void) {
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 0) == 6);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 1) == 5);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 2) == 4);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 3) == 3);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 4) == 2);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 5) == 1);
    assert(roba_encoder_scroll_tail_multiplier(6, 6, 6) == 0);
    assert(roba_encoder_scroll_tail_multiplier(6, 4, 4) == 0);
}

int main(void) {
    test_threshold_boundaries();
    test_first_and_slow_detents_stay_precise();
    test_fast_guard_and_max_arm();
    test_four_x_ramps_after_two_matching_intervals();
    test_fast_streak_controls_each_ramp_stage();
    test_direction_change_resets_to_one();
    test_idle_gap_resets_to_one();
    test_medium_speed_disarms_inertia();
    test_tail_is_finite();
    puts("encoder scroll tests passed");
    return 0;
}
