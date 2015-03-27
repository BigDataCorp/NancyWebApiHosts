﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NancyHostLib
{
    public class ModuleContainer
    {
        public delegate object InstanceFactory (params object[] args);

        class ModuleInfo
        {
            public Type TypeInfo;
            public InstanceFactory Factory;
        }

        object padlock = new object ();

        HashSet<string> blackListedAssemblies = new HashSet<string> (StringComparer.OrdinalIgnoreCase) { "mscorlib", "vshost", "BigDataPipeline", "NLog", "Newtonsoft.Json", "Topshelf", "Topshelf.Linux", "Topshelf.NLog", "BigDataPipeline.Web", "Nancy", "AWSSDK", "Dapper", "Mono.CSharp", "Mono.Security", "NCrontab", "Renci.SshNet", "System.Net.FtpClient", "MongoDB.Bson", "MongoDB.Driver", "System.Data.SQLite", "System.Net.Http.Formatting", "System.Web.Razor", "Microsoft.Owin.Hosting", "Microsoft.Owin", "Owin" };

        Dictionary<string, Assembly> loadedAssemblies;
        List<Assembly> validAssemblies = new List<Assembly> (30);

        Dictionary<string, List<Type>> exportedTypesByBaseType = new Dictionary<string, List<Type>> (StringComparer.Ordinal);

        Dictionary<string, ModuleInfo> exportedModules = new Dictionary<string, ModuleInfo> (StringComparer.Ordinal);

        NLog.Logger _logger = NLog.LogManager.GetLogger ("ModuleContainer");

        static ModuleContainer _instance = new ModuleContainer ();

        /// <summary>
        /// Gets the singleton instance for the ModuleContainer.
        /// </summary>
        /// <value>The instance.</value>
        public static ModuleContainer Instance
        {
            get
            {
                return _instance;
            }
        }

        /// <summary>
        /// Initializes the specified modules folder.<para/>
        /// Will load all assemblies found in the modules folder and subfolders and
        /// scan for the types derived from listOfInterfaces.
        /// </summary>
        /// <param name="modulesFolder">List of folders where plugin/modules are located.</param>
        /// <param name="listOfInterfaces">The list of interfaces or base types.</param>
        public void LoadModules (string[] modulesFolder, Type[] listOfInterfaces = null)
        {
            lock (padlock)
            {
                ContainerInitialization (modulesFolder, listOfInterfaces);
            }
        }

        /// <summary>
        /// Initializes the specified modules folder.<para/>
        /// Will load all assemblies found in the modules folder and subfolders and
        /// scan for the types derived from listOfInterfaces.
        /// </summary>
        /// <param name="modulesFolder">The modules folder.</param>
        /// <param name="listOfInterfaces">The list of interfaces or base types.</param>
        public void LoadModules (string modulesFolder, Type[] listOfInterfaces = null)
        {
            lock (padlock)
            {
                ContainerInitialization (new string[] { modulesFolder }, listOfInterfaces);
            }
        }

        private void ContainerInitialization (string[] modulesFolder, Type[] listOfInterfaces)
        {
            if (loadedAssemblies == null)
                loadedAssemblies = new Dictionary<string, Assembly> (StringComparer.Ordinal);

            // get base folder
            var baseAddress = AppDomain.CurrentDomain.BaseDirectory;
            // prepare extension folder
            if (modulesFolder == null || modulesFolder.Length == 0 || (modulesFolder.Length == 1 && String.IsNullOrEmpty (modulesFolder[0])))
                modulesFolder = new string[] { baseAddress };

            // get mscorelib assembly
            //Assembly mscorelib = 333.GetType ().Assembly;

            // load current assemblies code to register their types and avoid duplicity
            if (loadedAssemblies.Count == 0)
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies ())
                {
                    if (!a.GlobalAssemblyCache && !a.IsDynamic)
                    {
                        loadedAssemblies[a.FullName] = a;
                        if (!blackListedAssemblies.Contains (ParseAssemblyName (a.FullName)))
                            validAssemblies.Add (a);
                    }
                }

                // register assembly resolution for our loaded modules
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            }

            HashSet<string> invalidAssemblies = null;
            HashSet<string> loadedFolders = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

            // check the directory exists
            foreach (var folder in modulesFolder)
            {
                var directoryInfo = new DirectoryInfo (prepareFilePath (folder));
                if (!directoryInfo.Exists && loadedFolders.Contains (directoryInfo.FullName))
                {
                    continue;
                }
                loadedFolders.Add (directoryInfo.FullName);

                // read all files in modules folder, looking for assemblies
                // let's read all files, since linux use case sensitive search wich could lead
                // to case problems like ".dll" and ".Dll"            
                foreach (var file in directoryInfo.EnumerateFiles ("*", SearchOption.AllDirectories))
                {
                    // check if file has a valid assembly extension
                    if (!file.Extension.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) &&
                        !file.Extension.EndsWith (".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // try to get assembly full name: "Assembly text name, Version, Culture, PublicKeyToken"
                    // lets ignore it if we are unable to load it (access denied, invalid assembly or native code, etc...)
                    if (invalidAssemblies != null && invalidAssemblies.Contains (file.Name))
                        continue;
                    string assemblyName = null;
                    try
                    {
                        assemblyName = AssemblyName.GetAssemblyName (file.FullName).FullName;
                    }
                    catch (BadImageFormatException badImage)
                    {
                        if (invalidAssemblies == null)
                            invalidAssemblies = new HashSet<string> (StringComparer.Ordinal);
                        invalidAssemblies.Add (file.Name);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn ("Assembly ignored. Load error: " +
                            ex.GetType ().Name + ", location: " + file.FullName.Replace (baseAddress, "./").Replace ('\\', '/'));
                        continue;
                    }

                    // check if assembly was already loaded
                    if (loadedAssemblies.ContainsKey (assemblyName))
                        continue;

                    // try to load assembly
                    try
                    {
                        // load assembly
                        var a = Assembly.LoadFile (file.FullName);

                        // register in our loaded assemblies lookup map for AppDomain.CurrentDomain.AssemblyResolve resolution
                        loadedAssemblies.Add (assemblyName, a);
                        if (!blackListedAssemblies.Contains (ParseAssemblyName (assemblyName)))
                            validAssemblies.Add (a);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn (ex);
                    }
                }
            }

            // try to load derived types
            SearchForImplementations (listOfInterfaces);
        }

        private void SearchForImplementations (Type[] listOfInterfaces)
        {
            // sanity check
            if (listOfInterfaces == null || listOfInterfaces.Length == 0)
                return;

            // check if first assembly scan was executed
            if (loadedAssemblies == null)
                ContainerInitialization (null, listOfInterfaces);

            // prepare list of loaded interfaces
            for (int i = 0; i < listOfInterfaces.Length; i++)
            {
                if (!exportedTypesByBaseType.ContainsKey (listOfInterfaces[i].FullName))
                {
                    exportedTypesByBaseType[listOfInterfaces[i].FullName] = new List<Type> ();
                }
            }

            // try to load derived types
            try
            {
                List<Type> implementationsList;
                ModuleInfo info;

                // search listed assemblies
                foreach (var a in validAssemblies)
                {
                    // dynamic assemblies don't have GetExportedTypes method
                    if (a.IsDynamic)
                        continue;

                    // try to list public types
                    // only .net 4.5+ has this method implemented!
                    Type[] types = null;
                    try
                    {
                        types = a.GetExportedTypes ();
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn (ex);
                    }

                    // search for types derived from desired types list (listOfInterfaces)
                    for (int i = 0; i < types.Length; i++)
                    {
                        Type t = types[i];
                        if (t == null || t.IsAbstract || t.IsGenericTypeDefinition || !t.IsClass) //t.IsInterface
                            continue;
                        for (int j = 0; j < listOfInterfaces.Length; j++)
                        {
                            if (listOfInterfaces[j].IsAssignableFrom (t))
                            {
                                // register type in exportedModules map
                                if (!exportedModules.TryGetValue (t.FullName, out info))
                                {
                                    info = new ModuleInfo { TypeInfo = t };
                                    // register type by fullName (namespace + name) and name
                                    exportedModules[t.FullName] = info;
                                    exportedModules[t.Name] = info;
                                }

                                // add to interface list of types                                
                                if (!exportedTypesByBaseType.TryGetValue (listOfInterfaces[j].FullName, out implementationsList))
                                {
                                    implementationsList = new List<Type> ();
                                    exportedTypesByBaseType[listOfInterfaces[j].FullName] = implementationsList;
                                }
                                implementationsList.Add (t);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn (ex);
            }
        }

        private Assembly CurrentDomain_AssemblyResolve (object sender, ResolveEventArgs args)
        {
            Assembly a;
            if (!loadedAssemblies.TryGetValue (args.Name, out a))
                throw new InvalidOperationException (
                      String.Format ("Assembly not available in plugin/modules path; assembly name '{0}'.", args.Name));
            return a;
        }

        private static string ParseAssemblyName (string name)
        {
            int i = name.IndexOf (',');
            return (i > 0) ? name.Substring (0, i) : name;
        }

        /// <summary>
        /// Adjust file path.
        /// </summary>
        private string prepareFilePath (string path)
        {
            if (path == null)
                return path;
            int ix = path.IndexOf ("${basedir}", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                int tagLen = "${basedir}".Length;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (path.Length > ix + tagLen && (path[ix + tagLen] == '\\' || path[ix + tagLen] == '/'))
                    appDir = appDir.EndsWith ("/") || appDir.EndsWith ("\\") ? appDir.Substring (0, appDir.Length - 1) : appDir;
                path = path.Remove (ix, tagLen);
                path = path.Insert (ix, appDir);
            }
            path = path.Replace ("\\", "/");
            return path;
        }

        /// <summary>
        /// Creates the type constructor delegate for a type.
        /// </summary>
        /// <typeparam name="T">The type to create a constructor.</typeparam>
        /// <returns></returns>
        private InstanceFactory CreateFactory<T> ()
        {
            return CreateFactory (typeof (T));
        }

        /// <summary>
        /// Creates the type constructor delegate for a type.
        /// </summary>
        /// <param name="type">The type to create a constructor.</param>
        /// <returns></returns>
        private InstanceFactory CreateFactory (Type type)
        {
            return CreateFactory (type.GetConstructors ().OrderByDescending (i => i.GetParameters ().Length).First ());
        }

        /// <summary>
        /// Creates the type constructor delegate for a type..
        /// </summary>
        /// <param name="ctor">The constructor information.</param>
        /// <returns></returns>
        private InstanceFactory CreateFactory (ConstructorInfo ctor)
        {
            // code http://rogeralsing.com/2008/02/28/linq-expressions-creating-objects/
            Type type = ctor.DeclaringType;
            ParameterInfo[] paramsInfo = ctor.GetParameters ();
            // prepare parameters            
            //create a single param of type object[]
            var param = Expression.Parameter (typeof (object[]), "args");

            var argsExp = new Expression[paramsInfo.Length];

            //pick each arg from the params array 
            //and create a typed expression of them
            for (int i = 0; i < paramsInfo.Length; i++)
            {
                var index = Expression.Constant (i);
                Type paramType = paramsInfo[i].ParameterType;

                var paramAccessorExp = Expression.ArrayIndex (param, index);

                var paramCastExp = Expression.Convert (paramAccessorExp, paramType);

                argsExp[i] = paramCastExp;
            }

            //make a NewExpression that calls the
            //ctor with the args we just created
            var newExp = Expression.New (ctor, argsExp);

            //create a lambda with the New
            //Expression as body and our param object[] as arg
            var lambda = Expression.Lambda (typeof (InstanceFactory), newExp, param);

            //compile it
            var compiled = (InstanceFactory)lambda.Compile ();
            return compiled;
        }

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <typeparam name="T">The type of the T.</typeparam>
        /// <param name="fullTypeName">Full name of the type.</param>
        /// <returns></returns>
        public T GetInstanceAs<T> (string fullTypeName) where T : class
        {
            return GetInstance (fullTypeName) as T;
        }

        public T GetInstanceAs<T> (Type type) where T : class
        {
            return GetInstance (type.FullName) as T;
        }

        /// <summary>
        /// Gets an instance for a registered type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public object GetInstance (Type type)
        {
            return GetInstance (type.FullName);
        }

        /// <summary>
        /// Gets an instance for a registered type by its full type name.
        /// </summary>
        /// <param name="fullTypeName">Full name of the type, namespace and class name.</param>
        /// <returns></returns>
        public object GetInstance (string fullTypeName)
        {
            ModuleInfo module;
            // if we have the type, lets get it
            if (exportedModules.TryGetValue (fullTypeName, out module))
            {
                if (module.Factory == null)
                {
                    module.Factory = CreateFactory (module.TypeInfo);
                }
                return module.Factory ();
            }
            return null;
        }

        /// <summary>
        /// Gets an instance that implements the desired type T.
        /// </summary>
        /// <param name="type">The base type.</param>
        /// <returns></returns>
        public T GetInstanceOf<T> () where T : class
        {
            return GetInstancesOf<T> ().FirstOrDefault ();
        }

        /// <summary>
        /// Gets instances for all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of intances of registered types</returns>
        public IEnumerable<T> GetInstancesOf<T> () where T : class
        {
            foreach (var t in GetTypesOf<T> ())
                yield return GetInstanceAs<T> (t.FullName);
        }

        /// <summary>
        /// Gets all registered types for a given interface or base type.
        /// </summary>
        /// <typeparam name="T">The interface or base type.</typeparam>
        /// <returns>List of registered types</returns>
        public IEnumerable<Type> GetTypesOf<T> () where T : class
        {
            List<Type> list;
            // load interface implementations
            if (!exportedTypesByBaseType.TryGetValue (typeof (T).FullName, out list))
            {
                // if none was found, it was not initilized yet...
                SearchForImplementations (new[] { typeof (T) });
                // after the interface initilization, check again...
                if (!exportedTypesByBaseType.TryGetValue (typeof (T).FullName, out list))
                    yield break;
            }
            // return implementations
            for (int i = 0; i < list.Count; i++)
                yield return list[i];
        }

        /// <summary>
        /// Gets the constructor for the desired type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        public InstanceFactory GetConstructor (Type type)
        {
            return GetConstructor (type.FullName);
        }

        /// <summary>
        /// Gets the constructor for the desired type.
        /// </summary>
        /// <param name="fullTypeName">Full name of the type.</param>
        /// <returns></returns>
        public InstanceFactory GetConstructor (string fullTypeName)
        {
            ModuleInfo module;
            // if we have the type, lets get it
            if (exportedModules.TryGetValue (fullTypeName, out module))
            {
                if (module.Factory == null)
                {
                    module.Factory = CreateFactory (module.TypeInfo);
                }
                return module.Factory;
            }
            return null;
        }
    }
}