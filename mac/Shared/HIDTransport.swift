// Based on menu-bar-nape-pro-status by krgpi (MIT License)
// https://github.com/krgpi/menu-bar-nape-pro-status
// See mac/THIRD_PARTY_NOTICES.md

import Foundation
import IOKit
import IOKit.hid

public final class HIDTransport: @unchecked Sendable {
    public let device: IOHIDDevice
    private var reportBuffer = [UInt8](repeating: 0, count: 128)
    private var incomingContinuation: AsyncStream<Data>.Continuation?

    public private(set) lazy var incomingReports: AsyncStream<Data> = {
        let transport = self
        return AsyncStream { continuation in
            transport.incomingContinuation = continuation
            continuation.onTermination = { @Sendable _ in
                transport.incomingContinuation = nil
            }
        }
    }()

    public init(device: IOHIDDevice) {
        self.device = device
    }

    public func open() throws {
        IOHIDDeviceScheduleWithRunLoop(device, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        let result = IOHIDDeviceOpen(device, IOOptionBits(kIOHIDOptionsTypeNone))
        guard result == kIOReturnSuccess else {
            IOHIDDeviceUnscheduleFromRunLoop(device, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
            throw HIDError.openFailed(result)
        }
        registerInputReportCallback()
    }

    public func close() {
        incomingContinuation?.finish()
        IOHIDDeviceUnscheduleFromRunLoop(device, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        IOHIDDeviceClose(device, IOOptionBits(kIOHIDOptionsTypeNone))
    }

    public func sendReport(_ data: Data) throws {
        let result = data.withUnsafeBytes { buffer -> IOReturn in
            guard let pointer = buffer.baseAddress?.assumingMemoryBound(to: UInt8.self) else {
                return kIOReturnBadArgument
            }
            return IOHIDDeviceSetReport(
                device,
                kIOHIDReportTypeOutput,
                0,
                pointer,
                data.count
            )
        }
        guard result == kIOReturnSuccess else {
            throw HIDError.sendFailed(result)
        }
    }

    private func registerInputReportCallback() {
        reportBuffer.withUnsafeMutableBufferPointer { buffer in
            IOHIDDeviceRegisterInputReportCallback(
                device,
                buffer.baseAddress!,
                buffer.count,
                { context, _, _, _, reportID, report, reportLength in
                    guard let context else { return }
                    let transport = Unmanaged<HIDTransport>.fromOpaque(context).takeUnretainedValue()
                    let data = Data(bytes: report, count: reportLength)
                    transport.incomingContinuation?.yield(data)
                },
                Unmanaged.passUnretained(self).toOpaque()
            )
        }
    }

    public var vendorID: Int {
        hidProperty(kIOHIDVendorIDKey) ?? 0
    }

    public var productID: Int {
        hidProperty(kIOHIDProductIDKey) ?? 0
    }

    public var productName: String {
        hidProperty(kIOHIDProductKey) ?? "Unknown"
    }

    public var usagePage: Int {
        hidProperty(kIOHIDPrimaryUsagePageKey) ?? 0
    }

    public var usage: Int {
        hidProperty(kIOHIDPrimaryUsageKey) ?? 0
    }

    private func hidProperty<T>(_ key: String) -> T? {
        IOHIDDeviceGetProperty(device, key as NSString) as? T
    }
}

public enum HIDError: Error, LocalizedError {
    case openFailed(IOReturn)
    case sendFailed(IOReturn)
    case deviceNotFound

    public var errorDescription: String? {
        switch self {
        case .openFailed(let code): "Failed to open HID device (0x\(String(code, radix: 16)))"
        case .sendFailed(let code): "Failed to send HID report (0x\(String(code, radix: 16)))"
        case .deviceNotFound: "Nape Pro device not found"
        }
    }
}
