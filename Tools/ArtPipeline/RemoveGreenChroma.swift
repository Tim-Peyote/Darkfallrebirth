import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("RemoveGreenChroma: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 3 else {
    fail("usage: swift RemoveGreenChroma.swift <green-source.png> <alpha-output.png>")
}

let inputURL = URL(fileURLWithPath: CommandLine.arguments[1])
let outputURL = URL(fileURLWithPath: CommandLine.arguments[2])
guard let source = CGImageSourceCreateWithURL(inputURL as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
    fail("cannot decode input")
}

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

for index in stride(from: 0, to: pixels.count, by: 4) {
    let red = Double(pixels[index])
    let green = Double(pixels[index + 1])
    let blue = Double(pixels[index + 2])
    let dominance = green - max(red, blue)
    let brightness = green

    // The source matte is a highly saturated green. Key by both hue dominance and
    // brightness so teal water, moss, poison and dim emerald highlights survive.
    let hueKeep = max(0.0, min(1.0, (76.0 - dominance) / 58.0))
    let brightKeep = max(0.0, min(1.0, (112.0 - brightness) / 36.0))
    let alpha = max(hueKeep, brightKeep)
    if alpha <= 0.001 {
        pixels[index] = 0; pixels[index + 1] = 0; pixels[index + 2] = 0; pixels[index + 3] = 0
        continue
    }

    // Remove green fringe only from partially keyed pixels. The opaque interior is
    // untouched, which is important for the drowned and charnel biome palettes.
    let edge = 1.0 - alpha
    let neutralGreen = max(red, blue) * 0.72
    let cleanGreen = green * (1.0 - edge * 0.9) + neutralGreen * edge * 0.9
    pixels[index] = UInt8(max(0, min(255, red * alpha)))
    pixels[index + 1] = UInt8(max(0, min(255, cleanGreen * alpha)))
    pixels[index + 2] = UInt8(max(0, min(255, blue * alpha)))
    pixels[index + 3] = UInt8(max(0, min(255, 255.0 * alpha)))
}

guard let context = CGContext(data: &pixels, width: width, height: height,
                              bitsPerComponent: 8, bytesPerRow: width * 4,
                              space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue),
      let result = context.makeImage(),
      let destination = CGImageDestinationCreateWithURL(outputURL as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create output")
}
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
