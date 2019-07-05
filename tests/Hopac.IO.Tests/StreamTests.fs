namespace Hopac.IO.Tests

module Stream =

  open FsUnit
  open NUnit.Framework
  open System.Text
  open Hopac
  open Hopac.IO.Stream
  open System.IO
  open FsCheck

  [<TestFixture>]
  type Runner() = 

    let testString = "Hello, World!"
    let testArray = Encoding.UTF8.GetBytes testString

    let encodings = 
      [ Encoding.ASCII
        Encoding.BigEndianUnicode
        Encoding.Unicode
        Encoding.UTF32
        Encoding.UTF7 
        Encoding.UTF8 
        Encoding.Default ]

    let createStream array = 
      let str = new MemoryStream()
      str.Write (array, 0, array.Length)
      str.Position <- 0L
      str

    [<Test>] 
    member _x.``Stream.ReadToEndJob should encode with UTF8 by default and return same string`` () =
      let utf8IsDefault (str: string) =
        if isNull str then true else //Workaround for Encoding.GetBytes

        let testUtf8Array = Encoding.UTF8.GetBytes str
        use memStream     = createStream testUtf8Array
        let strContent    = memStream.ReadToEndJob() |> run
        strContent = str

      Check.QuickThrowOnFailure utf8IsDefault

    [<Test>] 
    member _x.``Stream.ReadToEndJob should return same string with all encodings`` () =
      let isSameString (str: string) =
        if isNull str then true else //Workaround for Encoding.GetBytes

        encodings
        |> List.forall (fun enc ->
          let arr = enc.GetBytes str
          use memStream = createStream arr
          let strContent = memStream.ReadToEndJob(enc) |> run
          strContent = str)
      Check.QuickThrowOnFailure isSameString

    [<Test>] 
    member _x.``Stream.ReadJob should read bytes`` () =
      use memStream = createStream testArray
      let strContent = memStream.ReadToEndJob() |> run

      strContent
      |> should equal testString

    [<Test>] 
    member _x.``Stream.WriteJob should write bytes`` () =
      use memStream = new MemoryStream()
      memStream.WriteJob(testArray, 0, testArray.Length) |> run

      memStream.Position
      |> should equal testArray.Length

    [<Test>] 
    member _x.``StreamReader.ReadToEndJob should encode with UTF8 by default and return same string`` () =
      let utf8IsDefault (str: string) =
        if isNull str then true else //Workaround for Encoding.GetBytes

        let testUtf8Array = Encoding.UTF8.GetBytes str
        use memStream     = createStream testUtf8Array
        use reader        = new StreamReader(memStream)
        let strContent    = reader.ReadToEndJob() |> run
        strContent = str

      Check.QuickThrowOnFailure utf8IsDefault

    [<Test>] 
    member _x.``StreamReader.ReadToEndJob should return same string with all encodings`` () =
      let isSameString (str: string) =
        if isNull str then true else //Workaround for Encoding.GetBytes

        encodings
        |> List.forall (fun enc ->
          let arr = enc.GetBytes str
          use memStream = createStream arr
          use reader        = new StreamReader(memStream, enc)
          let strContent    = reader.ReadToEndJob() |> run
          strContent = str)
      Check.QuickThrowOnFailure isSameString
