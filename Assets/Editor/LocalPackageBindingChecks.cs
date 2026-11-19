using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo=UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

public static class LocalPackageBindingChecks
{
    static string output;
    static double started;
    static Task<bool> connection;
    static JObject result;
    static bool ready;

    static string Hash(string path)
    {
        using(var hash=SHA256.Create())
        using(var stream=File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }

    public static void Run()
    {
        output=Environment.GetEnvironmentVariable("MAGICMR_PACKAGE_PROBE_OUTPUT");
        if(string.IsNullOrEmpty(output)||!Directory.Exists(output))throw new Exception("Explicit probe report directory missing");
        started=EditorApplication.timeSinceStartup;
        try
        {
            var pins=JObject.Parse(File.ReadAllText("Tools/local-package-pins.json"));
            var packages=PackageInfo.GetAllRegisteredPackages();
            var checks=new JArray();
            foreach(var pin in pins["packages"])
            {
                string name=(string)pin["name"];
                var package=packages.Single(p=>p.name==name);
                if(package.version!=(string)pin["version"]||package.source!=PackageSource.Local)
                    throw new Exception("Wrong resolved package version/source: "+name);
                string json=Path.Combine(package.resolvedPath,"package.json");
                if(Hash(json)!=(string)pin["packageJsonSha256"])
                    throw new Exception("Resolved metadata hash differs: "+name);
                checks.Add(new JObject{["name"]=name,["version"]=package.version,["source"]=package.source.ToString(),
                    ["resolvedPath"]=package.resolvedPath,["assetPath"]=package.assetPath,["metadataSha256"]=Hash(json)});
            }
            var pico=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.XR.PXR.PXR_Manager")).FirstOrDefault(t=>t!=null);
            if(pico==null)throw new Exception("PICO runtime assembly not loaded");
            var mcp=typeof(MCPServiceLocator).Assembly;
            if(PackageInfo.FindForAssembly(mcp)?.name!="com.coplaydev.unity-mcp")
                throw new Exception("MCP assembly is not mapped to resolved package");
            result=new JObject{["status"]="PACKAGE_BINDINGS_VERIFIED_WAITING_MCP",["packages"]=checks,
                ["picoAssembly"]=pico.Assembly.GetName().Name,["mcpAssembly"]=mcp.GetName().Name,
                ["playerFilesOrSceneAssetsModified"]=false,["picoDeviceTest"]=false};
            File.WriteAllText(Path.Combine(output,"unity-package-bindings.json"),result.ToString(Formatting.Indented));
            var endpoint=new Uri(HttpEndpointUtility.GetBaseUrl());
            if(endpoint.Scheme!="http"||!endpoint.IsLoopback||endpoint.Port!=8080||!string.IsNullOrEmpty(endpoint.UserInfo))
                throw new Exception("Probe requires the explicit local MCP endpoint on port 8080; preferences left unchanged");
            connection=MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            EditorApplication.update+=Tick;
        }
        catch(Exception error){Fail(error);}
    }

    static void Tick()
    {
        try
        {
            if(EditorApplication.timeSinceStartup-started>90)throw new TimeoutException("Bounded MCP editor connection/probe timeout");
            if(!ready)
            {
                if(!connection.IsCompleted)return;
                if(connection.IsFaulted||!connection.Result)throw new Exception("MCP HTTP transport did not connect",connection.Exception);
                ready=true;
                var identity=typeof(MCPServiceLocator).Assembly.GetType("MCPForUnity.Editor.Helpers.ProjectIdentityUtility",true);
                string instance=(string)identity.GetMethod("GetProjectName").Invoke(null,null)+"@"+(string)identity.GetMethod("GetProjectHash").Invoke(null,null);
                File.WriteAllText(Path.Combine(output,"unity-mcp-ready.json"),new JObject{["ready"]=true,["projectPath"]=Path.GetFullPath("."),
                    ["instance"]=instance,
                    ["transport"]="Http",["scope"]="Owned editor package/transport readiness; no scene save"}.ToString());
            }
            var path=Path.Combine(output,"mcp-client-finished.json");
            if(!File.Exists(path))return;
            var client=JObject.Parse(File.ReadAllText(path));
            if((bool?)client["unitySceneQueryVerified"]!=true)throw new Exception("MCP client did not verify a real scene query");
            result["status"]="PACKAGE_BINDINGS_AND_REAL_MCP_SCENE_QUERY_VERIFIED";
            result["checkedAtUtc"]=DateTime.UtcNow.ToString("o");
            File.WriteAllText(Path.Combine(output,"unity-package-bindings.json"),result.ToString(Formatting.Indented));
            EditorApplication.update-=Tick;
            EditorApplication.Exit(0);
        }
        catch(Exception error){Fail(error);}
    }

    static void Fail(Exception error)
    {
        if(!string.IsNullOrEmpty(output))
            File.WriteAllText(Path.Combine(output,"unity-package-error.txt"),error.ToString());
        EditorApplication.update-=Tick;
        Debug.LogException(error);
        EditorApplication.Exit(2);
    }
}
