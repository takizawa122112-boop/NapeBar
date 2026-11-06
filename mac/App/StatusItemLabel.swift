import SwiftUI

/// メニューバーの表示。例: `62% BT`、`59% USB ↑ L1`
struct StatusItemLabel: View {
    let batteryLevel: Int?
    let connection: DeviceStateStore.Connection?
    let layer: Int?
    let orientation: OctaShiftOrientation?
    var showConnection: Bool
    var showLayer: Bool
    var showOrientation: Bool

    var body: some View {
        if connection == nil {
            Image(systemName: "computermouse")
        } else {
            Text(parts.joined(separator: " "))
                .monospacedDigit()
        }
    }

    private var parts: [String] {
        var parts = [batteryLevel.map { "\($0)%" } ?? "--%"]
        if showConnection, let connection {
            parts.append(connection.rawValue)
        }
        // レイヤーと向きはUSB-C / 2.4GHz接続のときだけ取れる。
        if showOrientation, let orientation {
            parts.append(orientation.arrow)
        }
        if showLayer, let layer {
            parts.append("L\(layer)")
        }
        return parts
    }
}
