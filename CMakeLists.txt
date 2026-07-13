# SPDX-License-Identifier: MIT

if(CONFIG_ZMK_INPUT_PROCESSOR_ROBA_SCROLL)
  target_sources(app PRIVATE src/input_processor_roba_scroll.c)
endif()
