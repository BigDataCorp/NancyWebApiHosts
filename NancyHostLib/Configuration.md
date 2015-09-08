# NancyHostLib

Application description

## How to run

The application parameters can set in 3 ways:

1. app.config
2. command line arguments
3. a file with json format parameters (local or web)


**Priority:** *command line arguments* **>** *external file* **>** *app.config*

### app.config
Just set the desired parameters in the `<appSettings>` area.

Example

```
  <appSettings>
    <add key="logFilename" value="${basedir}/log/myLogFile.log" />
    <add key="logLevel" value="Info" />
  </appSettings>
```


### command line arguments
Each parameter can overwrite the default value or the value in the app.config by passing as a command line parameter.

Example

```
  NancyHostLib.exe --logLevel="Info" --logFilename="log.txt"
```

**Note:** The help can also be summoned by passing the argument `--help`


### external file with json format parameters (local or web)

See [config](#config) parameter.

This parameter can be passed in the app.config or as a command line argument.
Address to an external file with a json format configuration options. At service start up the file will be loaded and parsed.

**Note:** This configuration takes precedence over the configurations found in the appSettings area of app.config file. 

External file format

```
{
    logLeve: "Info",
    logFilename: "log.txt"
}
```


Example

```
  NancyHostLib.exe --config="Info" --logFilename="log.txt"
```

**Note:** The help can also be summoned by passing the argument `--help`




## List of Parameters

### logFilename
`logFilename=<string>`

Default value: `${basedir}/log/ListAndDeleteS3Files.log`


### logLevel
`logLevel=<string>`

Default value: `Info`

* Trace
* Debug
* Info
* Warn
* Error
* Fatal


### config
`config=<string>`

Default value: `empty`

This parameter can be passed in the app.config or as a command line argument.

Address to an external file with a json format configuration options. At service start up the file will be loaded and parsed.
**Note:** This configuration takes precedence over the configurations found in the appSettings area of app.config file.

This address could be a local file system location or a web location. Examples:
* `http://somewhere.com/myconfiguration.json`
* `./myconfiguration.json`
* `c:\my configuration files\myconfiguration.json`


Also, a list of file can be provided as comma separated values. Example: 

```
"config": "http://somewhere.com/myconfiguration.json, c:\my configuration files\myconfiguration.json"
```

File format example

```
{
    "storageModule": "MongoDbStorageModule",
    "storageConnectionString": "..."
}
```


### configAbortOnError
`configAbortOnError=<boolean>`

Default value: `true`

If the server should tolerate and ignore external file configuration load or parse errors.


### waitForKeyBeforeExit
`waitForKeyBeforeExit=<boolean>`

Default value: `false`

If the console application should ask for user input at the end, before exiting the application.


### EnableAuthentication
`EnableAuthentication=<bool>`

Default value: `false`


### Authentication
Alias for `EnableAuthentication`

### PathsAnonimousAccess
`PathsAnonimousAccess=<string>`

Default value: ``

List of paths that will be allowed access without authentication check (routes without authentication).

If authentication is enabled (EnableAuthentication is `true`) and PathsAnonimousAccess has values, only the listed paths will not require authentication.

If authentication is disabled (EnableAuthentication is `false`) this configuration will not be checked.

**Notes:**
1. the last `/` is not required 
2. only full paths are supported

Example using CSV:
```
--PathsAnonimousAccess "/login,/login/resetpassword,/path1/p2/p3"
```

Example using json array:
```
--PathsAnonimousAccess "[\"/login\", \"/logout\",\"/path1/p2/p3\"]"
```


### PathsAuthAccess
`PathsAuthAccess=<string>`

Default value: ``

List of paths that will require user authentication to access.

If authentication is enabled (EnableAuthentication is `true`) and PathsAuthAccess is empty all paths will require authentication with the exception of the paths in PathsAnonimousAccess.

If authentication is enabled (EnableAuthentication is `true`) and PathsAuthAccess has values, only the listed paths will require authentication with the exception of the paths in PathsAnonimousAccess.

If authentication is disabled (EnableAuthentication is `false`) this configuration will not be checked.

**Notes:**
1. the last `/` is not required 
2. only full paths are supported

Example using CSV:
```
--PathsAuthAccess "/secure/path,/othersecurepath"
```

Example using json array:
```
--PathsAuthAccess "[\"/secure/path\", \"/logout\",\"/path1/p2/p3\"]"
```


### Module
`module=<string>`

Example:
`module='c:/temp/myCustomModule.dll'`

Will automatically load this file



### ModulesFolder
`ModulesFolder=<string>`

Default value: `${basedir}/modules/`

Or a file path: `${basedir}/modules/*Module.dll`



### DebugMode
`debugMode=<boolean>`

Default value: `false`

Enable nancy request tracing and disable view caching to allow html page to be edited and tested.
Userfull for testing.