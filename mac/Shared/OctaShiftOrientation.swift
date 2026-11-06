// Based on menu-bar-nape-pro-status by krgpi (MIT License)
// https://github.com/krgpi/menu-bar-nape-pro-status
// See mac/THIRD_PARTY_NOTICES.md

import Foundation

public enum OctaShiftOrientation: String, Codable, CaseIterable, Sendable {
    case north, northEast, east, southEast, south, southWest, west, northWest

    public var arrow: String {
        switch self {
        case .north: "↑"
        case .northEast: "↗"
        case .east: "→"
        case .southEast: "↘"
        case .south: "↓"
        case .southWest: "↙"
        case .west: "←"
        case .northWest: "↖"
        }
    }

    public static func fromAngleIndex(_ index: Int) -> OctaShiftOrientation? {
        guard allCases.indices.contains(index) else { return nil }
        return allCases[allCases.startIndex.advanced(by: index)]
    }

    public var systemImageName: String {
        switch self {
        case .north: "arrow.up"
        case .northEast: "arrow.up.right"
        case .east: "arrow.right"
        case .southEast: "arrow.down.right"
        case .south: "arrow.down"
        case .southWest: "arrow.down.left"
        case .west: "arrow.left"
        case .northWest: "arrow.up.left"
        }
    }

    public var displayName: String {
        switch self {
        case .north: "North"
        case .northEast: "North-East"
        case .east: "East"
        case .southEast: "South-East"
        case .south: "South"
        case .southWest: "South-West"
        case .west: "West"
        case .northWest: "North-West"
        }
    }
}
