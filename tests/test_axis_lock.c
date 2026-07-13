#include <assert.h>
#include <stdio.h>
#include "../src/roba_axis_lock.h"

static const struct roba_axis_lock_config cfg = {
    5, 8, 1, 1, 2, 200, 175, 8, 175, 1, 0
};

static void initial_x_is_flushed(void) {
    struct roba_axis_lock_state s = {0};
    assert(roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_X, 3, 0).action == ROBA_AXIS_WAIT);
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_Y, 1, 1);
    assert(r.action == ROBA_AXIS_OUTPUT && r.direction == ROBA_AXIS_X && r.value == 3);
}

static void initial_y_is_flushed(void) {
    struct roba_axis_lock_state s = {0};
    roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_X, 1, 0);
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_Y, -4, 1);
    assert(r.action == ROBA_AXIS_OUTPUT && r.direction == ROBA_AXIS_Y && r.value == -4);
}

static void immediate_event_is_not_lost(void) {
    struct roba_axis_lock_state s = {0};
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_Y, 220, 0);
    assert(r.action == ROBA_AXIS_OUTPUT && r.value == 220);
}

static void cross_axis_switch_recovers_pending(void) {
    struct roba_axis_lock_config switch_cfg = cfg;
    struct roba_axis_lock_state s = {0};
    switch_cfg.idle_reset_ms = 0;
    roba_axis_lock_feed(&s, &switch_cfg, ROBA_AXIS_X, 4, 0);
    roba_axis_lock_feed(&s, &switch_cfg, ROBA_AXIS_Y, 1, 1);
    struct roba_axis_lock_result r = {0};
    for (int i = 0; i < 8; i++) {
        r = roba_axis_lock_feed(&s, &switch_cfg, ROBA_AXIS_Y, 1, 200 + i);
    }
    assert(r.action == ROBA_AXIS_OUTPUT && r.direction == ROBA_AXIS_Y && r.value == 8);
}

static void idle_resets_sampling(void) {
    struct roba_axis_lock_state s = {0};
    roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_X, 5, 0);
    roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_Y, 1, 1);
    assert(roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_Y, 2, 300).action == ROBA_AXIS_WAIT);
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &cfg, ROBA_AXIS_X, 1, 301);
    assert(r.direction == ROBA_AXIS_Y && r.value == 2);
}

static void switch_threshold_retains_until_met(void) {
    struct roba_axis_lock_config c = cfg;
    struct roba_axis_lock_state s = {0};
    c.idle_reset_ms = 0;
    c.lock_ms = 0;
    c.lock_events = 0;
    c.switch_threshold = 3;
    roba_axis_lock_feed(&s, &c, ROBA_AXIS_X, 4, 0);
    roba_axis_lock_feed(&s, &c, ROBA_AXIS_Y, 1, 1);
    assert(roba_axis_lock_feed(&s, &c, ROBA_AXIS_Y, 2, 2).action == ROBA_AXIS_WAIT);
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &c, ROBA_AXIS_Y, 1, 3);
    assert(r.action == ROBA_AXIS_OUTPUT && r.value == 3);
}

static void free_mode_passes_input(void) {
    struct roba_axis_lock_config free_cfg = cfg;
    struct roba_axis_lock_state s = {0};
    free_cfg.mode = 1;
    struct roba_axis_lock_result r = roba_axis_lock_feed(&s, &free_cfg, ROBA_AXIS_X, -9, 0);
    assert(r.action == ROBA_AXIS_OUTPUT && r.value == -9);
}

int main(void) {
    initial_x_is_flushed();
    initial_y_is_flushed();
    immediate_event_is_not_lost();
    cross_axis_switch_recovers_pending();
    idle_resets_sampling();
    switch_threshold_retains_until_met();
    free_mode_passes_input();
    puts("axis-lock tests passed");
    return 0;
}
