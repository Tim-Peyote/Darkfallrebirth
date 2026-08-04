import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("TrimAlpha: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 4,
      let padding = Int(CommandLine.arguments[2]), padding >= 0 else {
    fail("usage: swift TrimAlpha.swift <alpha-source.png> <padding> <output.png>")
}

let inputURL = URL(fileURLWithPath: CommandLine.arguments[1])
let outputURL = URL(fileURLWithPath: CommandLine.arguments[3])
guard let source = CGImageSourceCreateWithURL(inputURL as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { fail("cannot decode input") }

let width = image.width
let height = image.height
var pixels = [UInt8](repeating: 0, count: width * height * 4)
guard pixels.withUnsafeMutableBytes({ bytes in
    guard let context = CGContext(data: bytes.baseAddress, width: width, height: height,
                                  bitsPerComponent: 8, bytesPerRow: width * 4,
                                  space: CGColorSpaceCreateDeviceRGB(),
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
    context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
    return true
}) else { fail("cannot render input") }

var minX = width, minY = height, maxX = -1, maxY = -1
for y in 0..<height {
    for x in 0..<width where pixels[(y * width + x) * 4 + 3] > 8 {
        minX = min(minX, x); minY = min(minY, y)
        maxX = max(maxX, x); maxY = max(maxY, y)
    }
}
guard maxX >= minX, maxY >= minY else { fail("input contains no visible pixels") }

minX = max(0, minX - padding); minY = max(0, minY - padding)
maxX = min(width - 1, maxX + padding); maxY = min(height - 1, maxY + padding)
let crop = CGRect(x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1)
guard let result = image.cropping(to: crop),
      let destination = CGImageDestinationCreateWithURL(outputURL as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot crop or create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
print("Trimmed \(width)x\(height) to \(result.width)x\(result.height)")
