namespace Giraffe.Website
open Microsoft.AspNetCore.Http

[<RequireQualifiedAccess>]
module Css =
    open System.IO
    open System.Text
    open NUglify

    type BundledCss =
        {
            Content : string
            Hash    : string
            Path    : string
        }
        static member FromContent (name: string) (content : string) =
            let hash = Hash.sha1 content
            {
                Content = content
                Hash    = hash
                Path    = sprintf "/%s.%s.css" name hash
            }

    let private getErrorMsg (errors : seq<UglifyError>) =
        let msg =
            errors
            |> Seq.fold (fun (sb : StringBuilder) t ->
                sprintf "Error: %s, File: %s" t.Message t.File
                |> sb.AppendLine
            ) (StringBuilder("Couldn't uglify content."))
        msg.ToString()

    let minify (css : string) =
        css
        |> Uglify.Css
        |> (fun res ->
            match res.HasErrors with
            | true  -> failwith (getErrorMsg res.Errors)
            | false -> res.Code)

    let getMinifiedContent (fileName : string) =
        fileName
        |> File.ReadAllText
        |> minify

    let getBundledContent (bundleName : string) (fileNames : string list) =
        let result =
            fileNames
            |> List.fold(
                fun (sb : StringBuilder) fileName ->
                    fileName
                    |> getMinifiedContent
                    |> sb.AppendLine
            ) (StringBuilder())
        result.ToString()
        |> BundledCss.FromContent bundleName

[<RequireQualifiedAccess>]
module Url =
    let create (route : string) =
        route.TrimStart [| '/' |]
        |> sprintf "%s/%s" Env.baseUrl

// ---------------------------------
// Views
// ---------------------------------

[<RequireQualifiedAccess>]
module Views =
    open System
    open Giraffe.ViewEngine

    let private twitterCard (key : string) (value : string) =
        meta [ _name (sprintf "twitter:%s" key); _content value ]

    let private openGraph (key : string) (value : string) =
        meta [ attr "property" (sprintf "og:%s" key); attr "content" value ]

    let private css (url : string) = link [ _rel "stylesheet"; _type "text/css"; _href url ]

    let private internalLink (path : string) (title : string) =
        a [ _href (Url.create path) ] [ str title ]

    let private externalLink (url : string) (title : string) =
        a [ _href url; _target "blank" ] [ str title ]

    let minifiedCss =
        Css.getBundledContent
            "bundle"
            [
                "Assets/Private/main.css"
            ]

    let private masterView
                    (subject : string option)
                    (permalink : string option)
                    (headerContent : XmlNode list option)
                    (bodyContent   : XmlNode list) =
        let websiteName = "Giraffe"
        let pageTitle =
            match subject with
            | Some s -> sprintf "%s - %s" s websiteName
            | None   -> websiteName
        html [] [
            head [] [
                // Metadata
                meta [ _charset "utf-8" ]
                meta [ _name "viewport"; _content "width=device-width, initial-scale=1.0" ]
                meta [ _name "description"; _content pageTitle ]

                // Title
                title [] [ encodedText pageTitle ]

                // Favicon
                link [ _rel "apple-touch-icon"; _sizes "180x180"; _href (Url.create "/apple-touch-icon.png") ]
                link [ _rel "icon"; _type "image/png"; _sizes "32x32"; _href (Url.create "/favicon-32x32.png") ]
                link [ _rel "icon"; _type "image/png"; _sizes "16x16"; _href (Url.create "/favicon-16x16.png") ]
                link [ _rel "manifest"; _href (Url.create "/manifest.json") ]
                link [ _rel "shortcut icon"; _href (Url.create "/favicon.ico") ]
                meta [ _name "apple-mobile-web-app-title"; _content websiteName ]
                meta [ _name "application-name"; _content  websiteName ]
                meta [ _name "theme-color"; _content  "#ffffff" ]

                if permalink.IsSome then
                    // Twitter card tags
                    twitterCard "card" "summary"
                    twitterCard "site" "@dustinmoris"
                    twitterCard "creator" "@dustinmoris"

                    // Open Graph tags
                    openGraph "title"        pageTitle
                    openGraph "url"          permalink.Value
                    openGraph "type"         "website"
                    openGraph "image"        (Url.create "/giraffe.png")
                    openGraph "image:alt"    websiteName
                    openGraph "image:width"  "1094"
                    openGraph "image:height" "729"

                // Google Fonts
                link [ _rel "preconnect"; _href "https://fonts.gstatic.com" ]
                link [ _href "https://fonts.googleapis.com/css2?family=Inter:wght@100;200;300;400;500;600;700;800;900&display=swap"; _rel "stylesheet" ]

                // Minified & bundled CSS
                css (Url.create minifiedCss.Path)

                // Google Analytics
                //if Env.isProduction then googleAnalytics

                // Additional (optional) header content
                if headerContent.IsSome then yield! headerContent.Value
            ]
            body [] [
                header [] [
                    div [ _id "inner-header" ] [
                        img [ _id "logo"; _src (Url.create "/giraffe.png")  ]
                    ]
                ]
                nav [] [
                    div [ _id "inner-nav" ] [
                        ul [ _id "nav-links" ] [
                            li [] [ internalLink "/" "Home" ]
                            li [] [ internalLink "/docs" "Documentation" ]
                            li [] [ internalLink "/view-engine" "View Engine" ]
                            li [] [ externalLink "https://github.com/giraffe-fsharp" "GitHub"]
                            li [] [ externalLink "https://github.com/giraffe-fsharp/Giraffe/releases" "Releases" ]
                        ]
                    ]
                ]
                main [] bodyContent
                footer [] [
                    div [ _id "inner-footer" ] [
                        h5 [] [ rawText (sprintf "Copyright &copy; %i, %s" DateTime.Now.Year "Dustin Moris Gorski") ]
                        p [] [
                            rawText (sprintf "All content on this website, such as text, graphics, logos and images is the property of the Giraffe open source project and Dustin Moris Gorski.")
                        ]
                    ]
                ]
            ]
        ]

    let markdownView (title : string) (permalink : string) (content : XmlNode) =
        masterView (Some title) (Some permalink) None [ content ]

// ---------------------------------
// Markdown Parsing
// ---------------------------------

[<RequireQualifiedAccess>]
module MarkDog =
    open Markdig
    open Markdig.Extensions.AutoIdentifiers

    let private pipeline =
        MarkdownPipelineBuilder()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UsePipeTables()
            .Build()

    let toHtml (value : string) =
        Markdown.ToHtml(value, pipeline)

// ---------------------------------
// Web app
// ---------------------------------

[<RequireQualifiedAccess>]
module WebApp =
    open System
    open System.Net.Http
    open Microsoft.Extensions.Logging
    open Microsoft.Net.Http.Headers
    open Giraffe
    open Giraffe.EndpointRouting
    open Giraffe.ViewEngine

    let private allowCaching (duration : TimeSpan) : HttpHandler =
        publicResponseCaching (int duration.TotalSeconds) (Some "Accept-Encoding")

    let private cssHandler : HttpHandler =
        let eTag = EntityTagHeaderValue.FromString false Views.minifiedCss.Hash
        validatePreconditions (Some eTag) None
        >=> allowCaching (TimeSpan.FromDays 365.0)
        >=> setHttpHeader "Content-Type" "text/css"
        >=> setBodyFromString Views.minifiedCss.Content

    let private markdownHandler
        (markdownUrl   : string)
        (title         : string)
        (permalink     : string)
        (lineStart     : int)
        (linkReplacements : Map<string, string>) : HttpHandler =
        fun next ctx ->
            task {
                let client = new HttpClient()
                let! allContent = client.GetStringAsync(markdownUrl)
                let content =
                    linkReplacements
                    |> (Map.fold(fun (c : string) key sub -> c.Replace(key, sub)) allContent)
                    |> fun c -> c.Replace(markdownUrl, permalink)
                    |> fun c -> c.Split([| Environment.NewLine |], StringSplitOptions.None)
                    |> Array.skip lineStart
                    |> String.concat Environment.NewLine
                let response =
                    content
                    |> MarkDog.toHtml
                    |> rawText
                    |> Views.markdownView title permalink
                    |> htmlView
                return! response next ctx
            }

    let linkReplacements =
        [
            "https://github.com/giraffe-fsharp/Giraffe/blob/main/README.md", (Url.create "/")
            "https://github.com/giraffe-fsharp/Giraffe/blob/main/DOCUMENTATION.md", (Url.create "/docs")
            "https://github.com/giraffe-fsharp/Giraffe.ViewEngine/blob/master/README.md", (Url.create "/view-engine")
        ] |> Map.ofList

    let private indexHandler =
        allowCaching (TimeSpan.FromDays(1.0)) >=>
        markdownHandler
            "https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/main/README.md"
            "Home"
            (Url.create "/")
            4
            linkReplacements

    let private docsHandler =
        allowCaching (TimeSpan.FromDays(1.0)) >=>
        markdownHandler
            "https://raw.githubusercontent.com/giraffe-fsharp/Giraffe/main/DOCUMENTATION.md"
            "Documentation"
            (Url.create "/docs")
            0
            linkReplacements

    let private viewEngineHandler =
        allowCaching (TimeSpan.FromDays(1.0)) >=>
        markdownHandler
            "https://raw.githubusercontent.com/giraffe-fsharp/Giraffe.ViewEngine/master/README.md"
            "View Engine"
            (Url.create "/view-engine")
            2
            linkReplacements

    let private pingPongHandler : HttpHandler =
        noResponseCaching >=> text "pong"

    let private versionHandler : HttpHandler =
        noResponseCaching
        >=> json {| version = Env.appVersion |}

    let endpoints =
        [
            GET_HEAD [
                routef "/bundle.%s.css" (fun _ -> cssHandler)
                route "/"            indexHandler
                route "/docs"        docsHandler
                route "/view-engine" viewEngineHandler
                route "/ping"        pingPongHandler
                route "/version"     versionHandler
            ]
        ]

    let notFound =
        "Not Found"
        |> text
        |> RequestErrors.notFound

    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

module Main =
    open System
    open System.Collections.Generic
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Giraffe
    open Giraffe.EndpointRouting
    open Logfella
    open Logfella.LogWriters
    open Logfella.Adapters
    open Logfella.AspNetCore

    let private muteFilter =
        Func<Severity, string, IDictionary<string, obj>, exn, bool>(
            fun severity msg data ex ->
                msg.StartsWith "The response could not be cached for this request")

    let private createLogWriter (ctx : HttpContext option) =
        match Env.isProduction with
        | false -> ConsoleLogWriter(Env.logSeverity).AsLogWriter()
        | true  ->
            let basic =
                GoogleCloudLogWriter
                    .Create(Env.logSeverity)
                    .AddServiceContext(
                        Env.appName,
                        Env.appVersion)
                    .UseGoogleCloudTimestamp()
                    .AddLabels(
                        dict [
                            "appName", Env.appName
                            "appVersion", Env.appVersion
                        ])
            let final =
                match ctx with
                | None     -> basic
                | Some ctx ->
                    basic
                        .AddHttpContext(ctx)
                        .AddCorrelationId(Guid.NewGuid().ToString("N"))
            Mute.When(muteFilter)
                .Otherwise(final)

    let private createReqLogWriter =
        Func<HttpContext, ILogWriter>(Some >> createLogWriter)

    let private toggleRequestLogging =
        Action<RequestLoggingOptions>(
            fun x -> x.IsEnabled <- Env.enableRequestLogging)

    let configureApp (appBuilder : IApplicationBuilder) =
        appBuilder
            .UseGiraffeErrorHandler(WebApp.errorHandler)
            .UseRequestScopedLogWriter(createReqLogWriter)
            .UseRequestLogging(toggleRequestLogging)
            .UseForwardedHeaders()
            .UseHttpsRedirection(Env.forceHttps, Env.domainName)
            .UseTrailingSlashRedirection()
            .UseStaticFiles()
            .UseResponseCaching()
            .UseResponseCompression()
            .UseRouting()
            .UseGiraffe(WebApp.endpoints)
            .UseGiraffe(WebApp.notFound)
        |> ignore

    let configureServices (services : IServiceCollection) =
        services
            .AddProxies(
                Env.proxyCount,
                Env.knownProxyNetworks,
                Env.knownProxies)
            .AddMemoryCache()
            .AddResponseCaching()
            .AddResponseCompression()
            .AddRouting()
            .AddGiraffe()
        |> ignore

    [<EntryPoint>]
    let main args =
        try
            Log.SetDefaultLogWriter(createLogWriter None)
            Logging.outputEnvironmentSummary Env.summary

            Host.CreateDefaultBuilder(args)
                .UseLogfella()
                .ConfigureWebHost(
                    fun webHostBuilder ->
                        webHostBuilder
                            .ConfigureSentry(
                                Env.sentryDsn,
                                Env.name,
                                Env.appVersion)
                            .UseKestrel(
                                fun k -> k.AddServerHeader <- false)
                            .UseContentRoot(Env.appRoot)
                            .UseWebRoot(Env.publicAssetsDir)
                            .Configure(configureApp)
                            .ConfigureServices(configureServices)
                            |> ignore)
                .Build()
                .Run()
            0
        with ex ->
            Log.Emergency("Host terminated unexpectedly.", ex)
            1