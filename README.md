# Hopac.IO

Extensions for standard IO operations with Hopac Jobs without `Task<T>` or `Async<T>` overhead.

In order to start the build process run

    > build.cmd // on windows

Paket and FAKE will do the rest. 
Build process described in build.fsx

## What's inside
    * Extensions
        * System.IO.Stream
            * ReadToEndJob
            * ReadJob
            * WriteJob
            * CopyToJob
        * System.IO.StreamReader
            * ReadToEndJob
        * System.Net.HttpWebRequest
            * GetResponseJob
            * GetRequestStreamJob

    * Standalone functions
        * Azure
            * getAdTokenJob (login to Azure ActiveDirectory and get token)
        * Datalake
            * uploadJob
            * downloadStreamJob
            * downloadStringJob
            * getFileListJob

## Requirements

    * .NET Standard 1.6

## Maintainer(s)

- [Ayrat Hudaygulov][ayratMail]

[ayratMail]: mailto:ayrat@hudaygulov.ru "Ayrat Hudaygulov email"