namespace Hopac.IO

[<AutoOpen>]
module Stream = 

    open System
    open System.Text
    open Hopac

    let internal defaultBufferSize = 4 * 1024
    let internal defaultEncoding   = Encoding.UTF8

    type System.IO.TextReader with

        ///**Description**
        ///Reads all characters from the current position to the end of the stream.
        member reader.ReadToEndJob() = Job.fromTask reader.ReadToEndAsync

        [<Obsolete("Do not pass an encoding; specify it at the construction of the text reader.")>]
        member reader.ReadToEndJob(?encoding: Encoding) =
            reader.ReadToEndJob()

    type System.IO.Stream with
        
        ///**Description**
        ///Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read
        ///**Parameters**
        ///  * `buffer` - An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.
        ///  * `offset` - Optional. The maximum number of bytes to be read from the current stream. Default value = 0.
        ///  * `count` - Optional. The maximum number of bytes to be read from the current stream. Default value = `buffer.Length`
        member stream.ReadJob (buffer: byte[], ?offset, ?count) = 
            job {
                let offset = defaultArg offset 0
                let count  = defaultArg count buffer.Length
                // TODO: Should we use Job.fromTask and stream.ReadAsync instead?
                return! Job.fromAsync <| stream.AsyncRead(buffer, offset, count)
            }

        ///**Description**
        ///Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        ///**Parameters**
        ///  * `buffer` - An array of bytes. This method copies `count` bytes from `buffer` to the current stream.
        ///  * `offset` - Optional. The zero-based byte offset in `buffer` at which to begin copying bytes to the current stream. Default value = 0.
        ///  * `count` - Optional. The number of bytes to be written to the current stream. Default value = `buffer.Length`
        member stream.WriteJob (buffer: byte[], ?offset, ?count) =
            job {
                let offset = defaultArg offset 0
                let count  = defaultArg count buffer.Length
                return! Job.fromAsync <| stream.AsyncWrite(buffer, offset, count)
            }
            

        [<Obsolete("Use TextReader.ReadToEndJob. Streams are supposed to contain binary data; not text.")>]
        member stream.ReadToEndJob(?encoding, ?bufferSize) =
            let encoding   = defaultArg encoding defaultEncoding
            let bufferSize = defaultArg bufferSize defaultBufferSize
            job {
                use sr = new System.IO.StreamReader(stream, encoding)
                return! sr.ReadToEndJob()
            }

        ///**Description**
        ///Asynchronously reads the bytes from the current stream and writes them to another stream.
        ///**Parameters**
        ///  * `destination` - The stream to which the contents of the current stream will be copied.
        ///  * `bufferSize` - optional bufferSize. Default - 4096 bytes
        member stream.CopyToJob(destination: System.IO.Stream, ?bufferSize: int) =
            let bufferSize = defaultArg bufferSize defaultBufferSize
            Alt.fromUnitTask <| fun ct -> stream.CopyToAsync(destination, bufferSize, ct)

    ///**Description**
    ///Reads all characters from the current position to the end of the stream.
    let readStream (stream: System.IO.Stream) = stream.ReadToEndJob()
