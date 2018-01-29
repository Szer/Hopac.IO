namespace Hopac.IO

module Stream = 

    open System.Text
    open Hopac
    open Hopac.Infixes

    let internal defaultBufferSize = 1024
    let internal defaultEncoding   = UTF8Encoding() :> Encoding

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
                return stream.Read(buffer, offset, count)
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
                stream.Write(buffer, offset, count)
            }
            
        ///**Description**
        ///Reads all characters from the current position to the end of the stream.
        ///**Parameters**
        ///  * `encoding` - Optional. Encoding in which output string will be presented. Default value = UTF8
        member stream.ReadToEndJob(?encoding: Encoding) =
            let encoding = defaultArg encoding defaultEncoding
            let sb = StringBuilder()
            let buffer = Array.zeroCreate defaultBufferSize
            let rec readInternal (sb: StringBuilder) =
                stream.ReadJob buffer
                >>= function
                | 0 -> Job.result sb
                | x -> 
                    encoding.GetChars buffer.[..x-1]
                    |> sb.Append
                    |> readInternal
            readInternal sb
            >>- string

        ///**Description**
        ///Asynchronously reads the bytes from the current stream and writes them to another stream.
        ///**Parameters**
        ///  * `destination` - The stream to which the contents of the current stream will be copied.
        member stream.CopyToJob(destination: System.IO.Stream) =
            let buffer = Array.zeroCreate defaultBufferSize
            let rec writeInternal () =
                stream.ReadJob buffer
                >>= function
                | 0 -> Job.unit()
                | x -> destination.WriteJob(buffer,0, x) >>= writeInternal
            writeInternal()

    type System.IO.StreamReader with

        //**Description**
        ///Reads all characters from the current position to the end of the stream.
        ///**Parameters**
        ///  * `encoding` - Optional. Encoding in which output string will be presented. Default value = UTF8
        member reader.ReadToEndJob(?encoding: Encoding) =
            reader.BaseStream.ReadToEndJob(defaultArg encoding defaultEncoding)