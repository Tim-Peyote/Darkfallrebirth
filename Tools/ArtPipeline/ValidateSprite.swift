import CoreGraphics
import Foundation
import ImageIO

guard CommandLine.arguments.count >= 2 else {
    FileHandle.standardError.write(Data("ValidateSprite: pass PNG paths\n".utf8))
    exit(1)
}

var failures = 0
for path in CommandLine.arguments.dropFirst() {
    let url = URL(fileURLWithPath: path) as CFURL
    guard let source = CGImageSourceCreateWithURL(url, nil),
          let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
        print("INVALID decode \(path)")
        failures += 1
        continue
    }
    guard image.width == 256, image.height == 256 else {
        print("INVALID size \(image.width)x\(image.height) \(path)")
        failures += 1
        continue
    }

    var pixels = [UInt8](repeating: 0, count: image.width * image.height * 4)
    let rendered = pixels.withUnsafeMutableBytes { bytes -> Bool in
        guard let context = CGContext(data: bytes.baseAddress,
                                      width: image.width, height: image.height,
                                      bitsPerComponent: 8, bytesPerRow: image.width * 4,
                                      space: CGColorSpaceCreateDeviceRGB(),
                                      bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { return false }
        context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
        return true
    }
    guard rendered else {
        print("INVALID render \(path)")
        failures += 1
        continue
    }

    let gutter = 2
    var opaqueBorderPixels = 0
    for y in 0..<image.height {
        for x in 0..<image.width where x < gutter || y < gutter || x >= image.width - gutter || y >= image.height - gutter {
            if pixels[(y * image.width + x) * 4 + 3] > 4 { opaqueBorderPixels += 1 }
        }
    }
    if opaqueBorderPixels > 0 {
        print("INVALID border \(opaqueBorderPixels) opaque pixels \(path)")
        failures += 1
    }
}

if failures > 0 { exit(1) }
print("Validated \(CommandLine.arguments.count - 1) sprites: 256x256 with a clean 2px alpha gutter.")
