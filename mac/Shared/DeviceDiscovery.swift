// Based on menu-bar-nape-pro-status by krgpi (MIT License)
// https://github.com/krgpi/menu-bar-nape-pro-status
// See mac/THIRD_PARTY_NOTICES.md

import Foundation
import IOKit
import IOKit.hid

public struct DiscoveredDevice: Sendable {
    public let transport: HIDTransport

    public var vendorID: Int { transport.vendorID }
    public var productID: Int { transport.productID }
    public var productName: String { transport.productName }
    public var usagePage: Int { transport.usagePage }
    public var usage: Int { transport.usage }
}

public final class DeviceDiscovery: @unchecked Sendable {
    public static let keychronVendorID = 0x3434

    private var manager: IOHIDManager?
    private var continuation: AsyncStream<[DiscoveredDevice]>.Continuation?
    private var pollTimer: Timer?

    public init() {}

    public func start() -> AsyncStream<[DiscoveredDevice]> {
        let manager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))
        self.manager = manager

        let match: NSDictionary = [kIOHIDVendorIDKey: Self.keychronVendorID]
        IOHIDManagerSetDeviceMatching(manager, match)
        IOHIDManagerScheduleWithRunLoop(manager, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        IOHIDManagerOpen(manager, IOOptionBits(kIOHIDOptionsTypeNone))

        let discovery = self
        let stream = AsyncStream<[DiscoveredDevice]> { continuation in
            discovery.continuation = continuation
            continuation.onTermination = { @Sendable _ in
                discovery.stop()
            }
        }

        DispatchQueue.main.async { [weak self] in
            self?.pollTimer = Timer.scheduledTimer(withTimeInterval: 2.0, repeats: true) { [weak self] _ in
                self?.poll()
            }
            self?.poll()
        }

        return stream
    }

    public func stop() {
        pollTimer?.invalidate()
        pollTimer = nil
        if let manager {
            IOHIDManagerClose(manager, IOOptionBits(kIOHIDOptionsTypeNone))
            IOHIDManagerUnscheduleFromRunLoop(manager, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        }
        manager = nil
        continuation?.finish()
    }

    private func poll() {
        guard let manager else {
            NSLog("[NapePro] poll: no manager")
            return
        }
        guard let cfSet = IOHIDManagerCopyDevices(manager) else {
            NSLog("[NapePro] poll: no devices")
            continuation?.yield([])
            return
        }
        let hidDevices = cfSet as! Set<IOHIDDevice>
        let devices = hidDevices.map { DiscoveredDevice(transport: HIDTransport(device: $0)) }
        continuation?.yield(devices)
    }
}
