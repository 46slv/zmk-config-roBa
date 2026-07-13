if(CONFIG_ROBA_STATUS)
  target_sources(app PRIVATE ${CMAKE_CURRENT_LIST_DIR}/../src/roba_status.c)
endif()

if(CONFIG_ROBA_ENCODER_SCROLL)
  target_sources(app PRIVATE ${CMAKE_CURRENT_LIST_DIR}/../src/behavior_roba_encoder_scroll.c)
endif()
