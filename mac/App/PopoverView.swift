import ServiceManagement
import SwiftUI

struct PopoverView: View {
    @Environment(DeviceStateStore.self) private var store
    @AppStorage("showConnection") private var showConnection = true
    @AppStorage("showLayer") private var showLayer = false
    @AppStorage("showOrientation") private var showOrientation = false
    @State private var launchAtLogin = SMAppService.mainApp.status == .enabled

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            Divider()
            statusSection
            if store.bluetoothAvailability == .unauthorized {
                bluetoothPermissionNotice
            }
            Divider()
            settings
            Divider()
            footer
        }
        .padding()
        .frame(width: 280)
    }

    private var header: some View {
        HStack(spacing: 10) {
            Image(systemName: "computermouse.fill")
                .font(.title2)
            VStack(alignment: .leading, spacing: 2) {
                Text("Nape Pro")
                    .font(.headline)
                Text(store.connection?.description ?? "未接続")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Circle()
                .fill(store.connection == nil ? Color.red : Color.green)
                .frame(width: 8, height: 8)
        }
    }

    private var statusSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            row("バッテリー", store.batteryLevel.map { "\($0)%" } ?? "—")
            if store.connection == .usb || store.connection == .receiver {
                row("レイヤー", store.activeLayer.map { "L\($0)" } ?? "—")
                row("向き", store.orientation.map { "\($0.arrow) \($0.displayName)" } ?? "—")
            } else if store.connection == .bluetooth {
                Text("レイヤーと向きは、USB-Cか2.4GHzレシーバーで接続したときだけ表示できます。")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            if let lastUpdated = store.lastUpdated {
                row("最終更新", lastUpdated.formatted(date: .omitted, time: .shortened))
            }
        }
    }

    private var bluetoothPermissionNotice: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Bluetoothの使用が許可されていないため、Bluetooth接続の残量を読めません。")
                .font(.caption)
                .fixedSize(horizontal: false, vertical: true)
            Button("Bluetoothの設定を開く") {
                if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Bluetooth") {
                    NSWorkspace.shared.open(url)
                }
            }
            .font(.caption)
        }
    }

    private var settings: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("メニューバーに表示")
                .font(.caption)
                .foregroundStyle(.secondary)
            Toggle("接続方式（BT / USB / 2.4G）", isOn: $showConnection)
            Toggle("向き", isOn: $showOrientation)
            Toggle("レイヤー", isOn: $showLayer)
            Divider().padding(.vertical, 4)
            Toggle("ログイン時に起動", isOn: $launchAtLogin)
                .onChange(of: launchAtLogin) { _, enabled in
                    setLaunchAtLogin(enabled)
                }
        }
        .toggleStyle(.checkbox)
        .font(.caption)
    }

    private var footer: some View {
        HStack {
            Button("今すぐ更新") { store.refresh() }
            Spacer()
            Text("v\(appVersion)")
                .font(.caption2)
                .foregroundStyle(.tertiary)
            Button("終了") { NSApplication.shared.terminate(nil) }
                .keyboardShortcut("q")
        }
        .font(.caption)
    }

    private func row(_ title: String, _ value: String) -> some View {
        HStack {
            Text(title).foregroundStyle(.secondary)
            Spacer()
            Text(value).monospacedDigit().fontWeight(.medium)
        }
    }

    private var appVersion: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "?"
    }

    private func setLaunchAtLogin(_ enabled: Bool) {
        do {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
        } catch {
            NSLog("[NapeBar] Launch at login change failed: %@", error.localizedDescription)
            launchAtLogin = SMAppService.mainApp.status == .enabled
        }
    }
}
