import SwiftUI

@main
struct NapeBarApp: App {
    @State private var store: DeviceStateStore
    @AppStorage("showConnection") private var showConnection = true
    @AppStorage("showLayer") private var showLayer = false
    @AppStorage("showOrientation") private var showOrientation = false

    init() {
        let store = DeviceStateStore()
        _store = State(initialValue: store)
        store.start()
    }

    var body: some Scene {
        MenuBarExtra {
            PopoverView()
                .environment(store)
        } label: {
            StatusItemLabel(
                batteryLevel: store.batteryLevel,
                connection: store.connection,
                layer: store.activeLayer,
                orientation: store.orientation,
                showConnection: showConnection,
                showLayer: showLayer,
                showOrientation: showOrientation
            )
        }
        .menuBarExtraStyle(.window)
    }
}
