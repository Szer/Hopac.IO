# Hopac.IO

[![Build Status](https://dev.azure.com/hopacio/Hopac.IO/_apis/build/status/Szer.Hopac.IO)](https://dev.azure.com/hopacio/Hopac.IO/_build/latest?definitionId=1)

[![Build status](https://ci.appveyor.com/api/projects/status/flxcwyfr6wwcbk6s?svg=true)](https://ci.appveyor.com/project/Szer/hopac-io)

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
            * appendJob
            * downloadStreamJob
            * downloadStringJob
            * getFileListJob
        * Stream
            * readStream

## Requirements

    * .NET Standard 1.6

## Maintainer(s)

- [Ayrat Hudaygulov][ayratMail]

[ayratMail]: mailto:ayrat@hudaygulov.ru "Ayrat Hudaygulov email"
