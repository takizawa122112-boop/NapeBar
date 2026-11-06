import Foundation
import IOKit.hid
import Observation

/// NapeBarの状態。USB-C / 2.4GHz はベンダーHID(0xFF60)、Bluetoothは標準Battery Serviceから読む。
/// 両方見えるときは、レイヤーと向きも取れるHID側を優先する。
@MainActor
@Observable
final class DeviceStateStore {
    enum Connection: String {
        case receiver = "2.4G"
        case usb = "USB"
        case bluetooth = "BT"

        var description: String {
            switch self {
            case .receiver: "2.4GHzレシーバーで接続中"
            case .usb: "USB-Cで接続中"
            case .bluetooth: "Bluetoothで接続中"
            }
        }
    }

    private(set) var connection: Connection?
    private(set) var batteryLevel: Int?
    private(set) var activeLayer: Int?
    private(set) var orientation: OctaShiftOrientation?
    private(set) var bluetoothAvailability: BLEBatteryReader.Availability = .unknown
    private(set) var lastUpdated: Date?

    /// Nape Pro本体と、Keychron Link-KM(2.4GHzレシーバー)。他のKeychron製キーボードには問い合わせない。
    static let napeProProductID = 0x0440
    static let receiverProductID = 0xD026
    private static let vendorUsagePage = 0xFF60
    private static let getCurrentLayer: UInt8 = 0xA3
    private static let batteryCommand: [UInt8] = [0xA7, 0x31]

    private let discovery = DeviceDiscovery()
    private let ble = BLEBatteryReader()
    private var discoveryTask: Task<Void, Never>?
    private var hidBatteryTask: Task<Void, Never>?
    private var vendorTransport: HIDTransport?
    private var hidConnection: Connection?
    private var hidBattery: Int?
    private var bleReading = BLEBatteryReader.Reading(isConnected: false, level: nil, name: nil)
    private var awaitingStateResponse = false

    init() {}

    func start() {
        guard discoveryTask == nil else { return }

        ble.onChange = { [weak self] reading, availability in
            guard let self else { return }
            if reading.level != nil, reading.level != self.bleReading.level || self.hidConnection == nil {
                self.lastUpdated = Date()
            }
            self.bleReading = reading
            self.bluetoothAvailability = availability
            self.recompute()
        }
        ble.start()

        let stream = discovery.start()
        discoveryTask = Task { @MainActor [weak self] in
            for await devices in stream {
                self?.handleHIDDevices(devices)
            }
        }
    }

    func refresh() {
        sendBatteryQuery()
        ble.refresh()
    }

    // MARK: - HID (USB-C / 2.4GHz)

    /// 2秒ごとに呼ばれる。毎回見直すので、起動後にUSBを挿し直しても自動で切り替わる。
    private func handleHIDDevices(_ devices: [DiscoveredDevice]) {
        let candidates = devices.filter {
            $0.usagePage == Self.vendorUsagePage
                && ($0.productID == Self.napeProProductID || $0.productID == Self.receiverProductID)
        }

        if let current = vendorTransport,
           !candidates.contains(where: { CFEqual($0.transport.device, current.device) }) {
            closeHID()
        }

        if vendorTransport == nil,
           let device = candidates.first(where: { $0.productID == Self.receiverProductID }) ?? candidates.first {
            openHID(device)
        }
        recompute()
    }

    private func openHID(_ device: DiscoveredDevice) {
        let transport = device.transport
        do {
            try transport.open()
        } catch {
            NSLog("[NapeBar] Failed to open vendor HID: %@", error.localizedDescription)
            return
        }
        vendorTransport = transport
        hidConnection = device.productID == Self.receiverProductID ? .receiver : .usb

        let reports = transport.incomingReports
        Task { @MainActor [weak self] in
            for await report in reports {
                self?.processReport(report)
            }
        }

        hidBatteryTask?.cancel()
        hidBatteryTask = Task { @MainActor [weak self] in
            while !Task.isCancelled {
                self?.sendBatteryQuery()
                try? await Task.sleep(for: .seconds(60))
            }
        }
        Task { @MainActor [weak self] in
            try? await Task.sleep(for: .seconds(1))
            self?.requestCurrentState()
        }
    }

    private func closeHID() {
        hidBatteryTask?.cancel()
        hidBatteryTask = nil
        vendorTransport?.close()
        vendorTransport = nil
        hidConnection = nil
        hidBattery = nil
        activeLayer = nil
        orientation = nil
    }

    private func send(_ bytes: [UInt8]) {
        guard let transport = vendorTransport else { return }
        var report = Data(count: 32)
        for (index, byte) in bytes.enumerated() { report[index] = byte }
        do {
            try transport.sendReport(report)
        } catch {
            NSLog("[NapeBar] HID send failed: %@", error.localizedDescription)
        }
    }

    private func sendBatteryQuery() {
        send(Self.batteryCommand)
    }

    private func requestCurrentState() {
        guard vendorTransport != nil else { return }
        awaitingStateResponse = true
        send([Self.getCurrentLayer])
    }

    private func processReport(_ data: Data) {
        let bytes = [UInt8](data)
        guard bytes.count >= 3 else { return }

        if bytes[0] == Self.getCurrentLayer && bytes.count >= 4 {
            activeLayer = max(0, Int(bytes[1]) - 1)
            if awaitingStateResponse {
                awaitingStateResponse = false
            } else {
                orientation = OctaShiftOrientation.fromAngleIndex(Int(bytes[2])) ?? orientation
            }
        } else if bytes[0] == Self.batteryCommand[0] && bytes[1] == Self.batteryCommand[1], bytes[2] <= 100 {
            hidBattery = Int(bytes[2])
            lastUpdated = Date()
        }
        recompute()
    }

    // MARK: - 表示用の状態をまとめる

    private func recompute() {
        if let hidConnection {
            connection = hidConnection
            batteryLevel = hidBattery
        } else if bleReading.isConnected {
            connection = .bluetooth
            batteryLevel = bleReading.level
        } else {
            connection = nil
            batteryLevel = nil
        }
    }
}
