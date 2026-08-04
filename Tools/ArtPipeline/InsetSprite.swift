import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("InsetSprite: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 4,
      let inset = Int(CommandLine.arguments[2]), inset >= 0, inset < 128 else {
    fail("usage: swift InsetSprite.swift <input-256.png> <inset-pixels> <output.png>")
}
let input = CommandLine.arguments[1]
let output = CommandLine.arguments[3]
guard let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: input) as CFURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil),
      image.width == 256, image.height == 256 else { fail("input must be a 256x256 PNG") }

let size = 256
guard let context = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8,
                              bytesPerRow: size * 4, space: CGColorSpaceCreateDeviceRGB(),
                              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else { fail("cannot create canvas") }
context.clear(CGRect(x: 0, y: 0, width: size, height: size))
context.interpolationQuality = .none
context.draw(image, in: CGRect(x: inset, y: inset, width: size - inset * 2, height: size - inset * 2))
guard let result = context.makeImage() else { fail("cannot render") }
guard let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: output) as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else { fail("cannot create output") }
CGImageDestinationAddImage(destination, result, nil)
guard CGImageDestinationFinalize(destination) else { fail("cannot write output") }
