CC ?= cc
CFLAGS ?= -std=c11 -Wall -Wextra -Werror -pedantic

TARGET := test_encoder_scroll

.PHONY: clean test

test: $(TARGET)
	./$(TARGET)

$(TARGET): test_encoder_scroll.c ../../src/roba_encoder_scroll_math.h
	$(CC) $(CFLAGS) -I../../src test_encoder_scroll.c -o $(TARGET)

clean:
	rm -f $(TARGET)
