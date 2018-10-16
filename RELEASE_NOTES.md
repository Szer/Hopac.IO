# ChangeLog

## 1.4.4 - 2018-10-16

* Fixed linter errors in README.md
* Added Azure DevOps build & release

## 1.4.3 - 2018-03-16

* Updated paket.bootstrapper.exe

## 1.4.2 - 2018-02-12

* fixed error when Datalake function downloadStream returns Ok with error inside

## 1.4.1 - 2018-02-12

* added optional bufferSize argument to ReadToEndJob, CopyToJob methods
* changed defaultBufferSize to 4096 bytes

## 1.4.0 - 2018-02-12

* added appendJob to DataLake namespace
* uploadJob now able to upload streams >30Mb (append continuations)
* Breaking changes:
  * All returns Choice1of2/Choice2of2 changed to Result.Ok/Result.Error for clarity

## 1.3.2 - 2018-02-05

* Fixed bug with getAdToken return type

## 1.3.1 - 2018-02-05

* Fixed bug with null WebResponse
* Added try-catches to some Azure jobs

## 1.3.0 - 2018-01-29

* Added DataLake folder listing
* Refactored job workflows to operators

## 1.2.0 - 2018-01-12

* Breaking changes in Datalake methods:
  * uploadStreamJob now just uploadJob with ability to upload either String or Stream
  * downloadContentJob renamed to downloadStringJob for consistency
  * downloadJobs now take tuple of 3 strings instead of record type

## 1.1.1 - 2018-01-10

* Added Stream tests

## 1.1.0 - 2018-01-10

* Fixed Datalake stream upload
* Switched to meaningful versioning (Minor will be bumped on functional updates, Build will be bumped on fixes)
* Fixed ProjectUrl

## 1.0.3 - 2018-01-09

* added:
  * Stream.WriteJob
  * Stream.CopyToJob
  * HttpWebRequest.GetRequestStreamJob
  * Azure.getAdTokenJob
  * Datalake.uploadStreamJob
  * Datalake.downloadStreamJob
  * Datalake.downloadContentJob

## 1.0.2 - 2017-12-21

* HttpWebRequest added

## 1.0.1 - 2017-12-21

* Streams added