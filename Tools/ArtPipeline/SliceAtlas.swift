import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data(("SliceAtlas: " + message + "\n").utf8))
    exit(1)
}

guard CommandLine.arguments.count == 7 else {
    fail("usage: swift SliceAtlas.swift <source.png> <x> <y-from-top> <width> <height> <output.png>")
}

let sourcePath = CommandLine.arguments[1]
guard let x = Int(CommandLine.arguments[2]),
      let y = Int(CommandLine.arguments[3]),
      let width = Int(CommandLine.arguments[4]),
      let height = Int(CommandLine.arguments[5]),
      width > 0, height > 0 else {
    fail("coordinates and dimensions must be integers; width and height must be positive")
}
let outputPath = CommandLine.arguments[6]

let sourceURL = URL(fileURLWithPath: sourcePath) as CFURL
guard let source = CGImageSourceCreateWithURL(sourceURL, nil),
      let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
    fail("cannot decode \(sourcePath)")
}
guard x >= 0, y >= 0, x + width <= image.width, y + height <= image.height else {
    fail("crop \(x),\(y),\(width),\(height) exceeds \(image.width)x\(image.height) source")
}

// CGImage cropping uses a top-left pixel origin for decoded raster data.
let rect = CGRect(x: x, y: y, width: width, height: height)
guard let cropped = image.cropping(to: rect), cropped.width == width, cropped.height == height else {
    fail("crop failed or returned an unexpected size")
}

let outputURL = URL(fileURLWithPath: outputPath)
try? FileManager.default.createDirectory(at: outputURL.deletingLastPathComponent(),
                                         withIntermediateDirectories: true)
guard let destination = CGImageDestinationCreateWithURL(outputURL as CFURL,
                                                        UTType.png.identifier as CFString, 1, nil) else {
    fail("cannot create \(outputPath)")
}
CGImageDestinationAddImage(destination, cropped, nil)
guard CGImageDestinationFinalize(destination) else {
    fail("cannot write \(outputPath)")
}
