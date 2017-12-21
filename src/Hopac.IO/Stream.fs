namespace Hopac.IO

[<AutoOpen>]
module Stream = 

    open System.Text
    open Hopac

    let private defaultBufferSize = 1024
    let private defaultEncoding   = UTF8Encoding() :> Encoding

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
        ///Reads all characters from the current position to the end of the stream.
        ///**Parameters**
        ///  * `encoding` - Optional. Encoding in which output string will be presented. Default value = UTF8
        member stream.ReadToEndJob(?encoding: Encoding) =
            job {
                let encoding = defaultArg encoding defaultEncoding
                let sb = StringBuilder()
                let buffer = Array.zeroCreate defaultBufferSize
                let rec readInternal (sb: StringBuilder) =
                    job {
                        let! bytesRead = stream.ReadJob buffer
                        if   bytesRead = 0 then return sb else
                        let chars = encoding.GetChars buffer.[..bytesRead-1]
                        return! readInternal (sb.Append chars)
                    }                
                let! result = readInternal sb
                return result.ToString()
            }

    type System.IO.StreamReader with
        //**Description**
        ///Reads all characters from the current position to the end of the stream.
        ///**Parameters**
        ///  * `encoding` - Optional. Encoding in which output string will be presented. Default value = UTF8
        member reader.ReadToEndJob(?encoding: Encoding) =
            reader.BaseStream.ReadToEndJob(defaultArg encoding defaultEncoding)