import CoreBluetooth
import Foundation

/// Bluetooth接続のNape Proから、標準のBattery Service(0x180F)で残量を読む。
///
/// Bluetooth接続ではベンダーHID(0xFF60)が見えないため、macOSが接続済みの
/// Nape ProをCoreBluetoothで取り出し、Battery Level(0x2A19)を読み取り・購読する。
/// 書き込みは一切しない。
@MainActor
final class BLEBatteryReader: NSObject {
    enum Availability: Equatable {
        case unknown
        case ready
        case unauthorized
        case poweredOff
        case unsupported
    }

    struct Reading: Equatable {
        var isConnected: Bool
        var level: Int?
        var name: String?
    }

    nonisolated(unsafe) static let batteryService = CBUUID(string: "180F")
    nonisolated(unsafe) static let batteryLevel = CBUUID(string: "2A19")

    var onChange: ((Reading, Availability) -> Void)?

    private(set) var reading = Reading(isConnected: false, level: nil, name: nil)
    private(set) var availability: Availability = .unknown

    private var central: CBCentralManager?
    private var peripheral: CBPeripheral?
    private var levelCharacteristic: CBCharacteristic?
    private var scanTimer: Timer?
    private var readTimer: Timer?

    func start() {
        guard central == nil else { return }
        central = CBCentralManager(delegate: self, queue: .main)

        // Bluetoothを切り替えたときに拾えるよう、未接続の間は定期的に探し直す。
        scanTimer = Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.findConnectedNapePro() }
        }
        // 通知が来ない場合の保険として、接続中は60秒ごとに読み直す。
        readTimer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.refresh() }
        }
    }

    func refresh() {
        guard let peripheral, let levelCharacteristic, peripheral.state == .connected else { return }
        peripheral.readValue(for: levelCharacteristic)
    }

    private func findConnectedNapePro() {
        guard let central, central.state == .poweredOn, peripheral == nil else { return }
        let candidates = central.retrieveConnectedPeripherals(withServices: [Self.batteryService])
        guard let nape = candidates.first(where: { Self.isNapePro($0.name) }) else { return }

        peripheral = nape
        nape.delegate = self
        central.connect(nape)
    }

    private static func isNapePro(_ name: String?) -> Bool {
        guard let name else { return false }
        return name.localizedCaseInsensitiveContains("Nape Pro")
    }

    private func reset() {
        peripheral = nil
        levelCharacteristic = nil
        update(Reading(isConnected: false, level: nil, name: nil))
    }

    private func update(_ newReading: Reading) {
        reading = newReading
        onChange?(reading, availability)
    }

    private func setAvailability(_ state: CBManagerState) {
        switch state {
        case .poweredOn: availability = .ready
        case .unauthorized: availability = .unauthorized
        case .poweredOff: availability = .poweredOff
        case .unsupported: availability = .unsupported
        default: availability = .unknown
        }
        onChange?(reading, availability)
    }
}

extension BLEBatteryReader: CBCentralManagerDelegate {
    nonisolated func centralManagerDidUpdateState(_ central: CBCentralManager) {
        let state = central.state
        MainActor.assumeIsolated {
            setAvailability(state)
            if state == .poweredOn {
                findConnectedNapePro()
            } else {
                reset()
            }
        }
    }

    nonisolated func centralManager(_ central: CBCentralManager, didConnect peripheral: CBPeripheral) {
        MainActor.assumeIsolated {
            update(Reading(isConnected: true, level: reading.level, name: peripheral.name))
            peripheral.discoverServices([Self.batteryService])
        }
    }

    nonisolated func centralManager(_ central: CBCentralManager, didFailToConnect peripheral: CBPeripheral, error: Error?) {
        MainActor.assumeIsolated {
            NSLog("[NapeBar] BLE connect failed: %@", error?.localizedDescription ?? "unknown")
            reset()
        }
    }

    nonisolated func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peripheral: CBPeripheral, error: Error?) {
        MainActor.assumeIsolated { reset() }
    }
}

extension BLEBatteryReader: CBPeripheralDelegate {
    nonisolated func peripheral(_ peripheral: CBPeripheral, didDiscoverServices error: Error?) {
        MainActor.assumeIsolated {
            guard let service = peripheral.services?.first(where: { $0.uuid == Self.batteryService }) else {
                NSLog("[NapeBar] Battery Service not found on %@", peripheral.name ?? "?")
                return
            }
            peripheral.discoverCharacteristics([Self.batteryLevel], for: service)
        }
    }

    nonisolated func peripheral(_ peripheral: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        MainActor.assumeIsolated {
            guard let characteristic = service.characteristics?.first(where: { $0.uuid == Self.batteryLevel }) else {
                return
            }
            levelCharacteristic = characteristic
            peripheral.readValue(for: characteristic)
            if characteristic.properties.contains(.notify) {
                peripheral.setNotifyValue(true, for: characteristic)
            }
        }
    }

    nonisolated func peripheral(_ peripheral: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        guard characteristic.uuid == BLEBatteryReader.batteryLevel,
              let value = characteristic.value?.first,
              value <= 100 else { return }
        let level = Int(value)
        let name = peripheral.name
        MainActor.assumeIsolated {
            update(Reading(isConnected: true, level: level, name: name))
        }
    }
}
