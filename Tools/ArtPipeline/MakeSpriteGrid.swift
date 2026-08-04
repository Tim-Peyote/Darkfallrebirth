import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("MakeSpriteGrid: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 17 else {
    fail("usage: swift MakeSpriteGrid.swift <output.png> <15 input PNGs in row-major order>")
}

let cell = 256
let columns = 5
let rows = 3
let width = columns * cell
let height = rows * cell
let colorSpace = CGColorSpaceCreateDeviceRGB()
guard let context = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8,
                              bytesPerRow: width * 4, space: colorSpace,
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
    fail("cannot create canvas")
}
context.clear(CGRect(x: 0, y: 0, width: width, height: height))

for index in 0..<(columns * rows) {
    let path = CommandLine.arguments[index + 2]
    let url = URL(fileURLWithPath: path) as CFURL
    guard let source = CGImageSourceCreateWithURL(url, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
          image.width == cell, image.height == cell else {
        fail("invalid 256x256 input: \(path)")
    }
    let column = index % columns
    let rowFromTop = index / columns
    let drawRow = rows - rowFromTop - 1
    context.draw(image, in: CGRect(x: column * cell, y: drawRow * cell, width: cell, height: cell))
}

guard let result = context.makeImage() else { fail("cannot render grid") }
let output = URL(fileURLWithPath: CommandLine.arguments[1])
try? FileManager.default.createDirectory(at: output.deletingLastPathComponent(), withIntermediateDirectories: true)
guard let destination = CGImageDestinationCreateWithURL(output as CFURL, UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
