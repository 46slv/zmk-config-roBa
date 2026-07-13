CC      ?= cc
CFLAGS  ?= -Wall -Wextra -Wpedantic -std=c11 -O2

.PHONY: test clean

test: test_math test_axis_lock
	./test_math
	./test_axis_lock

test_math: test_math.c ../src/roba_scroll_math.h
	$(CC) $(CFLAGS) -o $@ test_math.c

test_axis_lock: test_axis_lock.c ../src/roba_axis_lock.h
	$(CC) $(CFLAGS) -o $@ test_axis_lock.c

clean:
	rm -f test_math test_axis_lock
