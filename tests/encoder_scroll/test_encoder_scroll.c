/* SPDX-License-Identifier: MIT */

#include <assert.h>
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>

#include "roba_encoder_scroll_math.h"

static const struct roba_encoder_scroll_config config = {
    .two_x_ms = 180,
    .four_x_ms = 100,
    .six_x_ms = 55,
    .reset_ms = 280,
    .fast_streak = 2,
    .inertia_streak = 3,
};

static struct roba_encoder_scroll_state fresh_state(void) {
    struct roba_encoder_scroll_state state;
    roba_encoder_scroll_state_reset(&state);
    return state;
}

static void test_threshold_boundaries(void) {
    assert(roba_encoder_scroll_requested_multiplier(&config, 54) == 6);
    assert(roba_encoder_scroll_requested_multiplier(&config, 55) == 4);
    assert(roba_encoder_scroll_requested_multiplier(&config, 99) == 4);
    assert(roba_encoder_scroll_requested_multiplier(&config, 100) == 2);
    assert(roba_encoder_scroll_requested_multiplier(&config, 179) == 2);
    assert(roba_encoder_scroll_requested_multiplier(&config, 180) == 1);
}

static void test_first_and_slow_detents_stay_precise(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1000).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1200).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1400).multiplier == 1);
    assert(!state.inertia_armed);
}

static void test_fast_guard_and_max_arm(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1040, 1080, 1120, 1160};
    const uint8_t expected[] = {1, 2, 6, 6, 6};

    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        struct roba_encoder_scroll_result result =
            roba_encoder_scroll_feed(&state, &config, 1, times[i]);
        assert(result.multiplier == expected[i]);
        assert(result.inertia_armed == (i == 4));
    }
}

static void test_four_x_requires_two_quick_intervals(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1000).multiplier == 1);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1080).multiplier == 2);
    assert(roba_encoder_scroll_feed(&state, &config, 1, 1160).multiplier == 4);
}

static void test_direction_change_resets_to_one(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1040, 1080, 1120, 1160};
    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        roba_encoder_scroll_feed(&state, &config, 1, times[i]);
    }

    struct roba_encoder_scroll_result reversed =
        roba_encoder_scroll_feed(&state, &config, -1, 1200);
    assert(reversed.multiplier == 1);
    assert(!reversed.inertia_armed);
}

static void test_idle_gap_resets_to_one(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    roba_encoder_scroll_feed(&state, &config, 1, 1000);
    roba_encoder_scroll_feed(&state, &config, 1, 1040);
    roba_encoder_scroll_feed(&state, &config, 1, 1080);

    struct roba_encoder_scroll_result after_gap =
        roba_encoder_scroll_feed(&state, &config, 1, 1360);
    assert(after_gap.multiplier == 1);
    assert(!after_gap.inertia_armed);
}

static void test_medium_speed_disarms_inertia(void) {
    struct roba_encoder_scroll_state state = fresh_state();
    const int64_t times[] = {1000, 1040, 1080, 1120, 1160};
    for (size_t i = 0; i < sizeof(times) / sizeof(times[0]); i++) {
        roba_encoder_scroll_feed(&state, &config, 1, times[i]);
    }
    assert(state.inertia_armed);

    struct roba_encoder_scroll_result medium =
        roba_encoder_scroll_feed(&state, &config, 1, 1280);
    assert(medium.multiplier == 2);
    assert(!medium.inertia_armed);
}

static void test_tail_is_finite(void) {
    assert(roba_encoder_scroll_tail_multiplier(4, 4, 0) == 4);
    assert(roba_encoder_scroll_tail_multiplier(4, 4, 1) == 3);
    assert(roba_encoder_scroll_tail_multiplier(4, 4, 2) == 2);
    assert(roba_encoder_scroll_tail_multiplier(4, 4, 3) == 1);
    assert(roba_encoder_scroll_tail_multiplier(4, 4, 4) == 0);
    assert(roba_encoder_scroll_tail_multiplier(4, 3, 3) == 0);
}

int main(void) {
    test_threshold_boundaries();
    test_first_and_slow_detents_stay_precise();
    test_fast_guard_and_max_arm();
    test_four_x_requires_two_quick_intervals();
    test_direction_change_resets_to_one();
    test_idle_gap_resets_to_one();
    test_medium_speed_disarms_inertia();
    test_tail_is_finite();
    puts("encoder scroll tests passed");
    return 0;
}
