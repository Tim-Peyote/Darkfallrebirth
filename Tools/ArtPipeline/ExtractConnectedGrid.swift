import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("ExtractConnectedGrid: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 5,
      let columns = Int(CommandLine.arguments[2]), columns > 0,
      let rows = Int(CommandLine.arguments[3]), rows > 0 else {
    fail("usage: swift ExtractConnectedGrid.swift <alpha-sheet.png> <columns> <rows> <output-directory>")
}

let input = CommandLine.arguments[1]
let outputDirectory = URL(fileURLWithPath: CommandLine.arguments[4], isDirectory: true)
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: input) as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fail("cannot decode input") }

let width = image.width
let height = image.height
var pixels = [UInt8](repeating: 0, count: width * height * 4)
let rendered = pixels.withUnsafeMutableBytes { bytes -> Bool in
    guard let context = CGContext(data: bytes.baseAddress, width: width, height: height,
                                  bitsPerComponent: 8, bytesPerRow: width * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    return true
}
guard rendered else { fail("cannot render input") }

struct Component {
    let indices: [Int]
    let minX: Int
    let minY: Int
    let maxX: Int
    let maxY: Int
    let centerX: Double
    let centerY: Double
}

var visited = [Bool](repeating: false, count: width * height)
var components = [Component]()
let neighbors = [(-1,-1),(0,-1),(1,-1),(-1,0),(1,0),(-1,1),(0,1),(1,1)]
for start in 0..<(width * height) {
    if visited[start] || pixels[start * 4 + 3] <= 8 { continue }
    visited[start] = true
    var queue = [start]
    var cursor = 0
    var indices = [Int]()
    var minX = width, minY = height, maxX = 0, maxY = 0
    while cursor < queue.count {
        let index = queue[cursor]
        cursor += 1
        indices.append(index)
        let x = index % width
        let y = index / width
        minX = min(minX, x); minY = min(minY, y)
        maxX = max(maxX, x); maxY = max(maxY, y)
        for (dx, dy) in neighbors {
            let nx = x + dx, ny = y + dy
            if nx < 0 || ny < 0 || nx >= width || ny >= height { continue }
            let next = ny * width + nx
            if visited[next] || pixels[next * 4 + 3] <= 8 { continue }
            visited[next] = true
            queue.append(next)
        }
    }
    if indices.count < 400 { continue }
    components.append(Component(indices: indices, minX: minX, minY: minY, maxX: maxX, maxY: maxY,
                                centerX: Double(minX + maxX) * 0.5,
                                centerY: Double(minY + maxY) * 0.5))
}

guard components.count >= columns * rows else {
    fail("found only \(components.count) substantial silhouettes, expected \(columns * rows)")
}

try? FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)
var used = Set<Int>()
let expectedWidth = Double(width) / Double(columns)
let expectedHeight = Double(height) / Double(rows)

for row in 0..<rows {
    for column in 0..<columns {
        let targetX = (Double(column) + 0.5) * expectedWidth
        let targetY = (Double(row) + 0.5) * expectedHeight
        var bestIndex = -1
        var bestScore = Double.infinity
        for index in components.indices where !used.contains(index) {
            let component = components[index]
            let dx = (component.centerX - targetX) / expectedWidth
            let dy = (component.centerY - targetY) / expectedHeight
            let score = dx * dx + dy * dy * 2.4
            if score < bestScore { bestScore = score; bestIndex = index }
        }
        guard bestIndex >= 0 else { fail("cannot assign row \(row), column \(column)") }
        used.insert(bestIndex)
        let component = components[bestIndex]

        let cellSize = 256
        let safeSize = 236
        let componentWidth = component.maxX - component.minX + 1
        let componentHeight = component.maxY - component.minY + 1
        let scale = min(1.0, min(Double(safeSize) / Double(componentWidth),
                                 Double(safeSize) / Double(componentHeight)))
        let destinationWidth = max(1, Int((Double(componentWidth) * scale).rounded()))
        let destinationHeight = max(1, Int((Double(componentHeight) * scale).rounded()))
        let destinationX = (cellSize - destinationWidth) / 2
        let destinationY = cellSize - 10 - destinationHeight

        var isolated = [UInt8](repeating: 0, count: componentWidth * componentHeight * 4)
        for sourceIndex in component.indices {
            let sx = sourceIndex % width
            let sy = sourceIndex / width
            let destinationIndex = ((sy - component.minY) * componentWidth + (sx - component.minX)) * 4
            let pixelIndex = sourceIndex * 4
            isolated[destinationIndex] = pixels[pixelIndex]
            isolated[destinationIndex + 1] = pixels[pixelIndex + 1]
            isolated[destinationIndex + 2] = pixels[pixelIndex + 2]
            isolated[destinationIndex + 3] = pixels[pixelIndex + 3]
        }
        guard let isolatedContext = CGContext(data: &isolated, width: componentWidth, height: componentHeight,
                                              bitsPerComponent: 8, bytesPerRow: componentWidth * 4,
                                              space: CGColorSpaceCreateDeviceRGB(),
                                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
              let isolatedImage = isolatedContext.makeImage(),
              let outputContext = CGContext(data: nil, width: cellSize, height: cellSize,
                                            bitsPerComponent: 8, bytesPerRow: cellSize * 4,
                                            space: CGColorSpaceCreateDeviceRGB(),
                                            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
            fail("cannot create output canvas")
        }
        outputContext.clear(CGRect(x: 0, y: 0, width: cellSize, height: cellSize))
        outputContext.interpolationQuality = .none
        outputContext.draw(isolatedImage, in: CGRect(x: destinationX, y: destinationY,
                                                     width: destinationWidth, height: destinationHeight))
        guard let result = outputContext.makeImage() else { fail("cannot render output") }
        let outputURL = outputDirectory.appendingPathComponent("r\(row)-c\(column).png")
        guard let destination = CGImageDestinationCreateWithURL(outputURL as CFURL,
                                                                UTType.png.identifier as CFString, 1, nil) else {
            fail("cannot create \(outputURL.path)")
        }
        CGImageDestinationAddImage(destination, result, nil)
        guard CGImageDestinationFinalize(destination) else { fail("cannot write \(outputURL.path)") }
    }
}

print("Extracted \(columns * rows) centered sprites from \(components.count) silhouettes.")
