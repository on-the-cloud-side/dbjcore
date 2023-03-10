#nullable enable
#define DO_NOT_CONSOLE
// above redirects Writeln to log.info and Writerr to log.error
// using System;
// using System.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Serilog;

//namespace dbj ;

// use like this:
// using static dbjcore;
// after which you can just use the method names 
// from the class utl in here
// without a class name and dot in front, for example:
// Writeln( Whoami() );

// kepp it in the global names space
// namespace dbjcore;

#region common utilities
internal sealed class DBJcore
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string my_domain(bool up_ = false)
    {
        Lazy<string> lazy_ = new Lazy<string>(
            () => Environment.UserDomainName.ToString()
        );
        return lazy_.Value;
    }

    /// <summary>
    /// Random word made from GUID
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string guid_word(bool up_ = false)
    {
        // wow, what a cludge, don't do this at home ;)
        // wait of 1 mili sec so that guids 
        // are sufficiently different
        System.Threading.Thread.Sleep(1);
        string input = Guid.NewGuid().ToString("N");
        input = up_ ? input.ToUpper() : input.ToLower();
        // remove numbers
        return Regex.Replace(input, @"[\d-]", string.Empty);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string local_now()
    {
        return DateTime.Now.ToLocalTime().ToString();
    }

    /// <summary>
    /// Return the name of the caller
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Whoami([CallerMemberName] string? caller_name = null)
    {
        if (string.IsNullOrEmpty(caller_name))
            return "unknown";
        if (string.IsNullOrWhiteSpace(caller_name))
            return "unknown";
        return caller_name;
    }

    /// <summary>
    /// Return the file name and source line number to the caller
    /// returned is a record type
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (string File, int Line) FileLineInfo(
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        // make and return a record
        return new (filePath, lineNumber );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Assert(bool condition_)
    {
        System.Diagnostics.Debug.Assert(condition_);
        return condition_;
    }

    /// <summary>
    /// Return the name of this assebly or executable
    /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ThisName()
    {
        return Assembly.GetExecutingAssembly().GetName().FullName;
    }

    /*
     * string test = "Testing 1-2-3";

    // convert string to stream
    byte[] byteArray = Encoding.ASCII.GetBytes(test);
    MemoryStream stream = new MemoryStream(byteArray);

    // convert stream to string
    StreamReader reader = new StreamReader(stream);
    string text = reader.ReadToEnd();
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.IO.Stream ToStream(string sval_)
    {
        byte[] byteArray = Encoding.ASCII.GetBytes(sval_);
        return new MemoryStream(byteArray);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToString(System.IO.Stream sval_)
    {
        StreamReader reader = new StreamReader(sval_);
        return reader.ReadToEnd();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToUTF8(string sval_)
    {
        byte[] bytes = Encoding.Default.GetBytes(sval_);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// retun the local IP as a string
    /// if "127.0.0.1" is returned obviously there is no network
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string local_ip()
    {
        Lazy<string> lazyIP = new Lazy<string>(
            () =>
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    try
                    {
                        socket.Connect("8.8.8.8", 65530);
                        IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint!;
                        return endPoint!.Address.ToString();
                    }
                    catch (Exception)
                    {
#if DEBUG
                        DBJLog.error($"No local IP found from the socket");
#endif
                        return "127.0.0.1";
                    }
                }
            }
        );

        return lazyIP.Value;
    }

} // Program_context

#endregion common utilities

///////////////////////////////////////////////////////////////////////////////////////
#region logging
///////////////////////////////////////////////////////////////////////////////////////

/*
$ dotnet add package Serilog
$ dotnet add package Serilog.Sinks.Console
$ dotnet add package Serilog.Sinks.File
*/
internal sealed class DBJLog
{
    public readonly static string text_line = "-------------------------------------------------------------------------------";
    public readonly static string app_friendly_name = AppDomain.CurrentDomain.FriendlyName;

    static string app_name
    {
        get
        {
            return app_friendly_name; //  app_friendly_name.Substring(0, app_friendly_name.IndexOf('.'));
        }
    }

#if LOG_TO_FILE
    // this path is obviously deeply wrong :P
    // ROADMAP: it will be externaly configurable
    public readonly static string log_file_path_template_ = "{0}logs\\{1}.log";
#endif
    public DBJLog()
    {
#if LOG_TO_FILE
        string log_file_path_ = string.Format(log_file_path_template_, AppContext.BaseDirectory, app_name);
#endif
        Serilog.Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
#if LOG_TO_FILE
           .WriteTo.File(log_file_path_, rollingInterval: RollingInterval.Day)
#else
           .WriteTo.Console()
#endif
           .CreateLogger();

#if LOG_TO_FILE
        DBJLog.log_file_header(log_file_path_);
#endif

    }

    ~DBJLog()
    {
        // this is very questionable
        Serilog.Log.CloseAndFlush();
    }

    #if LOG_TO_FILE
    static void log_file_header(string log_file_path_)
    {
        Serilog.Log.Information(text_line);
        Serilog.Log.Information($"[{System.DateTime.Now.ToLocalTime().ToString()}] Starting {app_name}");
        Serilog.Log.Information($"Launched from {Environment.CurrentDirectory}");
        Serilog.Log.Information($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
#if DEBUG
        Serilog.Log.Debug($"AppContext.BaseDir {AppContext.BaseDirectory}");
        Serilog.Log.Debug("This app is built in a DEBUG mode");
#endif
        Serilog.Log.Information(text_line);
        ProcessModule? pm_ = Process.GetCurrentProcess().MainModule;
        if (pm_ != null)
        {
            Serilog.Log.Information($"Runtime Call {Path.GetDirectoryName(pm_.FileName)}");
        }
        Serilog.Log.Information($"Log file location:{log_file_path_}");
        Serilog.Log.Information(text_line);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void debug_(string msg_) { Serilog.Log.Debug(msg_); 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void info_(string msg_) { Serilog.Log.Information(msg_); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    internal void error_(string msg_) { Serilog.Log.Error(msg_); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] 
    internal void fatal_(string msg_) { Serilog.Log.Fatal(msg_); }

    // this is where log instance is made on demand once and not before called the first time
    static Lazy<DBJLog> lazy_log = new Lazy<DBJLog>(() => new DBJLog());
    static public DBJLog logger { get { return lazy_log.Value; } }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void dispatch_( 
        Action<string> log_, string format, params object[] args
        )
    {
        if (args.Length < 1)
            log_(format);
        else
        {
            log_(string.Format(format, args));
        }
    }


    // calling one of these will lazy load the loger and then use it
    // repeated calls will reuse the same instance
    // log.info("where is this going then?");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void debug(string format, params object[] args) {
        if (Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Debug))
            dispatch_((msg_) => logger.debug_(msg_), format, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void info(string format, params object[] args)
    {
        if (Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
            dispatch_((msg_) => logger.info_(msg_), format, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void error(string format, params object[] args)
    {
        if (Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Error))
            dispatch_((msg_) => logger.error_(msg_), format, args);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void fatal(string format, params object[] args)
    {
        if (Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Fatal))
            dispatch_((msg_) => logger.fatal_(msg_), format, args);
    }
}

#endregion logging

///////////////////////////////////////////////////////////////////////////////////////
#region configuration
///////////////////////////////////////////////////////////////////////////////////////

/*

-------------------------------------------------------------------------------------------------------
We/I am use/ing json as config file format
------------------------------------------------------------------------------------------------------ -

Ditto in the proj file there must be information where is the config json file
and what is its name. Like so:
    
<ItemGroup>
<Content Include="appsettings.json">
<CopyToOutputDirectory>Always</CopyToOutputDirectory>
</Content>
</ItemGroup>

followed with section that adds the dot net components to use from the code

<ItemGroup>
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="6.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="7.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="7.0.0" />
</ItemGroup>
*/
internal sealed class DBJCfg
{
    IConfiguration config;
    readonly string config_file_name = string.Empty;
    /*
    config file name is completely 100% arbitrary
    it is hidden as def. val constructor parameter

    I know that is not a good thing, but I am lazy , and besides that
    How to configure the congiguration? Externaly? Probably using the cli arguments?
    */
    public DBJCfg(string json_config_file = "appsettings.json")
    {
        config_file_name = json_config_file;
        // Build a config object, using env vars and JSON providers.
        config = new ConfigurationBuilder()
      .AddJsonFile(json_config_file)
      .AddEnvironmentVariables()
      .Build();
    }

    // given path return value as string or empty string 
    public string kpv(string path_)
    {
        try
        {
            var section_ = this.config.GetRequiredSection(path_);
#if DBJ_TRACE
            Log.info($"cfg {DBJcore.Whoami()}() for path: '{path_}'  -- key: '{section_.Key}', val: '{section_.Value}'");
#endif
            return section_.Value!;
        }
        catch (Exception x_)
        {
#if DEBUG
            DBJLog.error($"No element in the cfg json found for the path: '{path_}'");
#endif
            DBJLog.error(x_.ToString());
        }
        return string.Empty;
    }

    /* 
    given path return the value or default value cast to the required type
    */
    T read<T>(string path_, T default_)
    {
        try
        {
            var section_ = this.config.GetRequiredSection(path_);
#if DBJ_TRACE
            Log.info($"cfg {DBJcore.Whoami()}() -- path: '{path_}'  key:'{section_.Key}', val: '{section_.Value}'");
#endif
            return section_.Get<T>();
        }
        catch (Exception x_)
        {
#if DEBUG
            DBJLog.error($"No element found for the path: '{path_}'");
#endif
            DBJLog.error(x_.ToString());
        }
        return default_; // not: default(T)!;
    }

    // this is where cfg is made on demand once 
    // and not before it is called for the first time
    static Lazy<DBJCfg> lazy_cfg = new Lazy<DBJCfg>(() => new DBJCfg());

    static public DBJCfg instance { get { return lazy_cfg.Value; } }
    //
    // calling will lazy load the Configurator and then use it
    //
    // var ip1 = Cfg.get<string>("ip1", "192.168.0.0");
    //
    public static T get<T>(string path_, T dflt_) { return instance.read<T>(path_, dflt_); }

    public static string FileName { get { return instance.config_file_name; } }

} // Config standardorum superiorum

#endregion configuration




