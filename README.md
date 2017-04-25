# TestSharpRunner #

TestSharpRunner is an NUnit3 based test runner. Its main purpose is to help with Selenium based functional UI testing.

It lets you define the browser, you'd like to run your tests on with command line parameter.

##How does it work?##

It works mostly the same as the NUnit3 Console Runner, only one change had been made:
You can add the optional parameter: --browser={selected_browser} or --b={selected_browser} to your command.

It'll create a browserconfig.txt file in the folder that contains your test dll file.

You can read the content of that file in your test and create a switch based on it to handle your driver setup.

##Example for your driver setup with this##

```
private string GetCurrentlySetBrowser
{

    string codeBase = Assembly.GetExecutingAssembly( ).CodeBase;
    UriBuilder uri = new UriBuilder( codeBase );
    string path = Uri.UnescapeDataString( uri.Path );
    string assemblyDirectory = Path.GetDirectoryName( path );
    
    get
    {
        string configTextInAssemblyDir = Path.Combine( assemblyDirectory, "browserconfig.txt" );

        if ( !File.Exists( configTextInAssemblyDir ) )
        {
            return "chrome";
        }
        else
        {
            string browserText = File.ReadAllText( configTextInAssemblyDir );
            browserText = browserText.Trim( ' ' );
            return browserText.ToLowerInvariant( );
        }
    }
}

private void InitCurrentlySetBrowser( string browser )
{
    switch ( browser )
    {
        case "firefox":
            driver = new FirefoxDriver( );
            driver.Manage( ).Window.Maximize( );
            break;
        case "edge":
            driver = new EdgeDriver( );
            driver.Manage( ).Window.Maximize( );
            break;
        case "ie":
        case "internet explorer":
        case "internetexplorer":
        case "iexplorer":
            driver = new InternetExplorerDriver( );
            driver.Manage( ).Window.Maximize( );
            break;
        case "safari":
            driver = new SafariDriver( );
            driver.Manage( ).Window.Maximize( );
            break;
        case "chrome":
        default:
            driver = new ChromeDriver( );
            driver.Manage( ).Window.Maximize( );
            break;
    }
}


[SetUp]
public void Setup()
{
    InitCurrentlySetBrowser( GetCurrentlySetBrowser );
}
```

##Other information##

Shootout to Charlie Poole and his team for this cool unit testing framework and console runner!
TestSharpRunner is mainly used for test automation, functional testing, but it's based heavily on their unit testing framework and test running tools.

You can use NUnit and NUnit Console Runner every functionality with TestSharpRunner, this is only an extension to the original.
For further information and capabilities please check NUnit's page: [NUnit3 wiki] (https://github.com/nunit/docs/wiki/NUnit-Documentation)


##NUnit original readme##

NUnit is a unit-testing framework for all .Net languages. Initially ported from JUnit, the current production release, version 3, has been completely rewritten with many new features and support for a wide range of .NET platforms.

#### License ####

NUnit is Open Source software and NUnit 3 is released under the [MIT license](http://www.nunit.org/nuget/nunit3-license.txt). Earlier releases used the [NUnit license](http://www.nunit.org/nuget/license.html). Both of these licenses allow the use of NUnit in free and commercial applications and libraries without restrictions.

#### Contributors ####

NUnit 3 was created by [Charlie Poole](https://github.com/CharliePoole), [Rob Prouse](https://github.com/rprouse), [Simone Busoli](https://github.com/simoneb), [Neil Colvin](https://github.con/oznetmaster) and numerous community contributors. A complete list of contributors since NUnit migrated to GitHub can be [found on GitHub](https://github.com/nunit/nunit-console/graphs/contributors).

Earlier versions of NUnit were developed by Charlie Poole, James W. Newkirk, Alexei A. Vorontsov, Michael C. Two and Philip A. Craig.
