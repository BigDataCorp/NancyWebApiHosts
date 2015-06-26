# Nancy ASP Host

Simple Asp.Net Host for Nancy Modules

## Configuration

### EnableAuthentication
`EnableAuthentication=<bool>`

Default value: `false`


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


### LogLevel
`LogLevel=<string>`

Default value: `Info`

* Trace
* Debug
* Info
* Warn
* Error
* Fatal

**Note:** Nancy exceptions will be logged only in Debug mode.


### LogFilename
`LogFilename=<string>`

Default value: `${basedir}/log/nancyhost.log`


### Config
`Config=<string>`

Default value: `empty`


Address to an external file with a json format configuration options. At service start up the file will be loaded and parsed.
**Note:** This configuration takes precedence over the configurations found in the appSettings area of web.config file.

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
    "LogLevel": "Trace",
    "storageConnectionString": "..."
}
```
