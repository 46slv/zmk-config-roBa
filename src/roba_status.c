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

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
#include <zephyr/device.h>
#include <zephyr/usb/usb_device.h>
#include <zephyr/usb/class/usb_hid.h>
#endif

#include <zmk/battery.h>
#include <zmk/event_manager.h>
#include <zmk/events/battery_state_changed.h>
#include <zmk/events/layer_state_changed.h>
#include <zmk/keymap.h>
#include <zmk/split/central.h>

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
#include <zmk/events/usb_conn_state_changed.h>
#include <zmk/usb.h>
#endif

LOG_MODULE_REGISTER(roba_status, CONFIG_ZMK_LOG_LEVEL);

#define ROBA_STATUS_PROTOCOL_VERSION 1
#define ROBA_STATUS_UNKNOWN_BATTERY UINT8_MAX

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
#define ROBA_STATUS_USB_REPORT_ID 1
#define ROBA_STATUS_USB_USAGE_PAGE 0xFF60
#define ROBA_STATUS_USB_USAGE 0x01
#endif

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

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
struct roba_status_usb_report {
    uint8_t report_id;
    struct roba_status_packet packet;
} __packed;

BUILD_ASSERT(sizeof(struct roba_status_usb_report) == 13,
             "roBa USB status report size changed");

static const uint8_t roba_status_usb_report_desc[] = {
    0x06, 0x60, 0xFF, // Usage Page (Vendor Defined 0xFF60)
    0x09, 0x01,       // Usage (0x01)
    0xA1, 0x01,       // Collection (Application)
    0x85, ROBA_STATUS_USB_REPORT_ID,
    0x15, 0x00,       // Logical Minimum (0)
    0x26, 0xFF, 0x00, // Logical Maximum (255)
    0x75, 0x08,       // Report Size (8 bits)
    0x95, 0x0C,       // Report Count (12 bytes)
    0x09, 0x02,       // Usage (Status snapshot)
    0x81, 0x02,       // Input (Data, Variable, Absolute)
    0xC0,             // End Collection
};

static const struct device *status_hid_dev;
static K_SEM_DEFINE(status_hid_sem, 1, 1);
static struct roba_status_usb_report status_usb_input_report;
static struct roba_status_usb_report status_usb_control_report;
#endif

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

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
#define HID_GET_REPORT_TYPE_MASK 0xff00
#define HID_GET_REPORT_ID_MASK 0x00ff
#define HID_REPORT_TYPE_INPUT 0x100

static void status_usb_in_ready(const struct device *dev) {
    ARG_UNUSED(dev);
    k_sem_give(&status_hid_sem);
}

static int status_usb_get_report(const struct device *dev, struct usb_setup_packet *setup,
                                 int32_t *len, uint8_t **data) {
    ARG_UNUSED(dev);

    if ((setup->wValue & HID_GET_REPORT_TYPE_MASK) != HID_REPORT_TYPE_INPUT ||
        (setup->wValue & HID_GET_REPORT_ID_MASK) != ROBA_STATUS_USB_REPORT_ID) {
        return -ENOTSUP;
    }

    status_usb_control_report.report_id = ROBA_STATUS_USB_REPORT_ID;
    populate_packet(&status_usb_control_report.packet);
    *len = sizeof(status_usb_control_report);
    *data = (uint8_t *)&status_usb_control_report;
    return 0;
}

static const struct hid_ops status_usb_ops = {
    .int_in_ready = status_usb_in_ready,
    .get_report = status_usb_get_report,
};

static bool status_usb_ready(void) {
    return status_hid_dev != NULL && zmk_usb_is_hid_ready();
}

static void notify_usb_status(const struct roba_status_packet *packet) {
    if (!status_usb_ready() || k_sem_take(&status_hid_sem, K_NO_WAIT) != 0) {
        return;
    }

    status_usb_input_report.report_id = ROBA_STATUS_USB_REPORT_ID;
    memcpy(&status_usb_input_report.packet, packet, sizeof(*packet));
    int ret = hid_int_ep_write(status_hid_dev, (uint8_t *)&status_usb_input_report,
                               sizeof(status_usb_input_report), NULL);
    if (ret != 0) {
        k_sem_give(&status_hid_sem);
        if (ret != -ENODEV && ret != -EAGAIN) {
            LOG_WRN("Failed to notify USB status: %d", ret);
        }
    }
}

static int roba_status_usb_init(void) {
    status_hid_dev = device_get_binding("HID_1");
    if (status_hid_dev == NULL) {
        LOG_ERR("Unable to locate roBa status HID device");
        return -ENODEV;
    }

    usb_hid_register_device(status_hid_dev, roba_status_usb_report_desc,
                            sizeof(roba_status_usb_report_desc), &status_usb_ops);
    return usb_hid_init(status_hid_dev);
}

SYS_INIT(roba_status_usb_init, APPLICATION, CONFIG_ZMK_USB_HID_INIT_PRIORITY);
#else
static bool status_usb_ready(void) { return false; }
#endif

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
    if (!notifications_enabled && !status_usb_ready()) {
        return;
    }

    struct roba_status_packet packet;
    sequence++;
    populate_packet(&packet);

    if (notifications_enabled) {
        int ret = bt_gatt_notify(NULL, &roba_status_service.attrs[1], &packet, sizeof(packet));
        if (ret < 0 && ret != -ENOTCONN) {
            LOG_WRN("Failed to notify BLE status: %d", ret);
        }
    }

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
    notify_usb_status(&packet);
#endif
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

#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
    if (as_zmk_usb_conn_state_changed(eh) != NULL) {
        notify_status();
    }
#endif

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
#if IS_ENABLED(CONFIG_ROBA_STATUS_USB)
ZMK_SUBSCRIPTION(roba_status, zmk_usb_conn_state_changed);
#endif
