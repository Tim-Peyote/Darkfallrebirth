import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("KeepMainSprite: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 3 else {
    fail("usage: swift KeepMainSprite.swift <input.png> <output.png>")
}

let input = CommandLine.arguments[1]
let output = CommandLine.arguments[2]
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: input) as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fail("input must be a PNG") }

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

var visited = [Bool](repeating: false, count: width * height)
var best = [Int]()
var bestScore = -Double.infinity
let center = CGPoint(x: Double(width - 1) * 0.5, y: Double(height - 1) * 0.5)
let neighbors = [(-1,-1),(0,-1),(1,-1),(-1,0),(1,0),(-1,1),(0,1),(1,1)]

for start in 0..<(width * height) {
    if visited[start] || pixels[start * 4 + 3] <= 8 { continue }
    visited[start] = true
    var queue = [start]
    var cursor = 0
    var component = [Int]()
    var sumX = 0.0
    var sumY = 0.0
    while cursor < queue.count {
        let index = queue[cursor]
        cursor += 1
        component.append(index)
        let x = index % width
        let y = index / width
        sumX += Double(x)
        sumY += Double(y)
        for (dx, dy) in neighbors {
            let nx = x + dx
            let ny = y + dy
            if nx < 0 || ny < 0 || nx >= width || ny >= height { continue }
            let next = ny * width + nx
            if visited[next] || pixels[next * 4 + 3] <= 8 { continue }
            visited[next] = true
            queue.append(next)
        }
    }
    let centroid = CGPoint(x: sumX / Double(component.count), y: sumY / Double(component.count))
    let dx = centroid.x - center.x
    let dy = centroid.y - center.y
    // Prefer the substantial silhouette closest to the cell center. Neighbor-frame fragments
    // enter from an edge and therefore receive a strong distance penalty.
    let score = Double(component.count) - (dx * dx + dy * dy) * 0.12
    if score > bestScore {
        bestScore = score
        best = component
    }
}

var keep = [Bool](repeating: false, count: width * height)
for index in best { keep[index] = true }
for index in 0..<(width * height) where !keep[index] {
    pixels[index * 4] = 0
    pixels[index * 4 + 1] = 0
    pixels[index * 4 + 2] = 0
    pixels[index * 4 + 3] = 0
}

guard let context = CGContext(data: &pixels, width: width, height: height,
                              bitsPerComponent: 8, bytesPerRow: width * 4,
                              space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
      let result = context.makeImage() else { fail("cannot create output image") }
guard let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: output) as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
