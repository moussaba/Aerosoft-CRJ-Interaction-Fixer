using AerosoftCRJInteractionFixer.Properties;
using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Xml;

namespace AerosoftCRJInteractionFixer
{
	class Program
	{
		static string OriginalPackageName = "aerosoft-crj";
		static string PatchPackageName = "aerosoft-crj-interaction-fix";

		static string OriginalPackageVersionRequirement_Community = "1.0.15";
		static string OriginalPackageVersionRequirement_Marketplace = "1.0.15";
		static string PatchPackageVersion = "1.0.0";

		static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
		{
			Encoder = JavaScriptEncoder.Create( UnicodeRanges.All ),
			WriteIndented = true
		};

		enum PackageSource
		{
			Community,
			Official
		}

		static void Main( string[] Args )
		{
			WriteWelcomeMessage();

			Log( $"Locating package '{ OriginalPackageName }'" );

			bool MarketplacePackage = false;

			// Validate that the CRJ exists in the community folder
			var OriginalPackagePath = GetPackagePath( PackageSource.Community, OriginalPackageName );
			if ( !Directory.Exists( OriginalPackagePath ) )
			{
				OriginalPackagePath = GetPackagePath( PackageSource.Official, OriginalPackageName );
				MarketplacePackage = true;
			}

			if ( !Directory.Exists( OriginalPackagePath ) )
			{
				LogError( $"Failed to locate package '{ OriginalPackageName }'. Please ensure the MSFS Aerosoft CRJ is installed prior to running this application." );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			Log( "Checking package dependencies" );

			// Ensure the version is 1.0.6 or 1.0.9, depending on the package source
			string OriginalPackageVersionRequirement = MarketplacePackage ? OriginalPackageVersionRequirement_Marketplace : OriginalPackageVersionRequirement_Community;

			var OriginalPackageManifestPath = Path.Combine( OriginalPackagePath, "manifest.json" );
			if ( !File.Exists( OriginalPackageManifestPath ) )
			{
				LogError( $"Unable to locate the { OriginalPackageName } package manifest file at location '{ OriginalPackageManifestPath }'." );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			var OriginalPackageManifest = JsonSerializer.Deserialize< Manifest >( File.ReadAllText( OriginalPackageManifestPath ) );
			if ( OriginalPackageManifest.PackageVersion != OriginalPackageVersionRequirement )
			{
				LogError( $"The Aerosoft CRJ must be version { OriginalPackageVersionRequirement }. Version { OriginalPackageManifest.PackageVersion } is currently installed." );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			// Clear out any previous patch packages
			var PatchPackagePath = GetPackagePath( PackageSource.Community, PatchPackageName );
			if ( Directory.Exists( PatchPackagePath ) )
			{
				Log( $"Removing existing instance of package '{ PatchPackageName }'" );
				DeleteDirectory( PatchPackagePath );
			}

			CreateDirectory( PatchPackagePath );

			Log( "Processing Model Behavior Defs" );
			if ( !ProcessModelBehaviorDefs( OriginalPackagePath, PatchPackagePath ) )
			{
				DeleteDirectory( PatchPackagePath );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			Log( "Processing 'CRJ550_Interior.xml' files" );
			if ( !ProcessModelBehaviors( OriginalPackagePath, PatchPackagePath, "Aerosoft_CRJ_550", "CRJ550_Interior.xml" ) )
			{
				DeleteDirectory( PatchPackagePath );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			Log( "Processing 'CRJ700_Interior.xml' files" );
			if ( !ProcessModelBehaviors( OriginalPackagePath, PatchPackagePath, "Aerosoft_CRJ_700", "CRJ700_Interior.xml" ) )
			{
				DeleteDirectory( PatchPackagePath );

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			Log("Processing 'CRJ900_Interior.xml' files");
			if (!ProcessModelBehaviors(OriginalPackagePath, PatchPackagePath, "Aerosoft_CRJ_900", "CRJ900_Interior.xml"))
			{
				DeleteDirectory(PatchPackagePath);

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			Log("Processing 'CRJ1000_Interior.xml' files");
			if (!ProcessModelBehaviors(OriginalPackagePath, PatchPackagePath, "Aerosoft_CRJ_1000", "CRJ1000_Interior.xml"))
			{
				DeleteDirectory(PatchPackagePath);

				WriteFailureMessage();
				WaitForExit();

				return;
			}

			GenerateLayout( PatchPackagePath );
			GenerateManifest( PatchPackagePath, OriginalPackageManifest, OriginalPackageVersionRequirement );

			WriteSuccessMessage();
			WaitForExit();
		}



		static bool ProcessModelBehaviorDefs( string OriginalPackagePath, string PatchPackagePath )
		{
			CreateDirectory( Path.Combine( PatchPackagePath, "ModelBehaviorDefs" ) );

			var OriginalPackageTemplatesPath = Path.Combine( OriginalPackagePath, @"ModelBehaviorDefs\ASCRJ_Templates.xml" );
			if ( !File.Exists( OriginalPackageTemplatesPath ) )
			{
				LogError( $"Required file '{ OriginalPackageTemplatesPath }' could not be found." );
				return false;
			}

			CopyFile( OriginalPackageTemplatesPath, Path.Combine( PatchPackagePath, @"ModelBehaviorDefs\ASCRJ_Templates.xml" ) );

			Log( $"Applying patch to { Path.Combine( PatchPackagePath, @"ModelBehaviorDefs\ASCRJ_Templates.xml" ) }" );
			File.AppendAllText( Path.Combine( PatchPackagePath, @"ModelBehaviorDefs\ASCRJ_Templates.xml" ), Resources.ASCRJ_Knob_Infinite_Push_Template );

			return true;
		}

		static bool ProcessModelBehaviors( string OriginalPackagePath, string PatchPackagePath, string AirplaneId, string ModelBehaviorFileName )
		{
			int ModelsProcessed = 0;

			var ModificationsList = JsonSerializer.Deserialize< ModelBehaviorModifications >( Resources.ModelBehaviorModifications );

			XmlReaderSettings ReaderSettings = new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment };
			XmlWriterSettings WriterSettings = new XmlWriterSettings { IndentChars = "\t", NewLineChars = "\r\n", Indent = true, Encoding = Encoding.UTF8 };

			var OriginalPackageModelDirectories = Directory.GetDirectories( Path.Combine( OriginalPackagePath, $@"SimObjects\Airplanes\{ AirplaneId }" ), "model*" );
			foreach ( var OriginalPackageModelDirectory in OriginalPackageModelDirectories )
			{
				var ModelFolderName = OriginalPackageModelDirectory.Substring( OriginalPackageModelDirectory.LastIndexOf( '\\' ) + 1 );

				Log( $"Processing model '{ ModelFolderName }'" );

				var ModelXmlPath = Path.Combine( OriginalPackageModelDirectory, ModelBehaviorFileName );
				if ( !File.Exists( ModelXmlPath ) )
				{
					LogError( $"Required file '{ ModelXmlPath }' could not be found." );
					continue;
				}

				var Reader = XmlReader.Create( ModelXmlPath, ReaderSettings );

				// Create the destination folder for the current model
				var PatchPackageModelDirectory = Path.Combine( PatchPackagePath, $@"SimObjects\Airplanes\{ AirplaneId }\{ ModelFolderName }" );
				CreateDirectory( PatchPackageModelDirectory );

				var StringWriter = new StringWriterUTF8();
				var XmlWriter = System.Xml.XmlWriter.Create( StringWriter, WriterSettings );

				XmlWriter.WriteStartDocument();
				XmlWriter.Flush();

				// The model behavior XML files are technically invalid XML so we need to write
				// this first 'root note' manually ahead of writing the rest of the XML document
				Reader.ReadToDescendant( "ModelInfo" );
				StringWriter.WriteLine();
				StringWriter.Write( Reader.ReadOuterXml() );

				// Load the rest of the file into an XmlDocument so we can start editing it
				Reader.ReadToDescendant( "ModelBehaviors" );

				var ModelBehaviorXML = new XmlDocument();
				ModelBehaviorXML.Load( Reader );

				foreach ( var ModificationEntry in ModificationsList.Modifications )
				{
					var ButtonNode = ModelBehaviorXML.SelectSingleNode( $"//Component[@ID='{ ModificationEntry.ButtonId }']" );
					ButtonNode.ParentNode.RemoveChild( ButtonNode );

					var KnobNode = ModelBehaviorXML.SelectSingleNode( $"//Component[@ID='{ ModificationEntry.KnobId }']" );
					var KnobNodeTemplate = KnobNode.FirstChild;

					KnobNodeTemplate.RemoveAll();

					var Attribute = ModelBehaviorXML.CreateAttribute( "Name" );
					Attribute.Value = "ASCRJ_Knob_Infinite_Push_Template";
					KnobNodeTemplate.Attributes.Append( Attribute );

					var KnobAnimNameNode = ModelBehaviorXML.CreateNode( "element", "KNOB_ANIM_NAME", "" );
					KnobAnimNameNode.InnerText = ModificationEntry.KnobAnimName;
					KnobNodeTemplate.AppendChild( KnobAnimNameNode );

					var KnobChangeNameNode = ModelBehaviorXML.CreateNode( "element", "KNOB_CHANGE_NAME", "" );
					KnobChangeNameNode.InnerText = ModificationEntry.KnobChangeName;
					KnobNodeTemplate.AppendChild( KnobChangeNameNode );

					var PushAnimNameNode = ModelBehaviorXML.CreateNode( "element", "PUSH_ANIM_NAME", "" );
					PushAnimNameNode.InnerText = ModificationEntry.PushAnimName;
					KnobNodeTemplate.AppendChild( PushAnimNameNode );

					var PushNameNode = ModelBehaviorXML.CreateNode( "element", "PUSH_NAME", "" );
					PushNameNode.InnerText = ModificationEntry.PushName;
					KnobNodeTemplate.AppendChild( PushNameNode );
				}

				ModelBehaviorXML.Save( XmlWriter );
				WriteFile( Path.Combine( PatchPackageModelDirectory, ModelBehaviorFileName ), StringWriter.ToString(), Encoding.UTF8 );

				++ModelsProcessed;
			}

			if ( ModelsProcessed == 0 )
			{
				LogError( $"Failed to process any models for airplane '{ AirplaneId }'" );
				return false;
			}

			return true;
		}

		static void GenerateLayout( string PatchPackagePath )
		{
			Log( "Creating package layout" );

			var PackageLayout = new Layout();

			var PackageFiles = Directory.GetFiles( PatchPackagePath, "*.*", SearchOption.AllDirectories );
			foreach ( var File in PackageFiles )
			{
				var PackageContent = new Content();
				var PackageFileInfo = new FileInfo( File );

				PackageContent.Path = Path.GetRelativePath( PatchPackagePath, File ).Replace( "\\", "/" );
				PackageContent.Size = PackageFileInfo.Length;
				PackageContent.Date = PackageFileInfo.LastWriteTimeUtc.ToFileTimeUtc();

				PackageLayout.Content.Add( PackageContent );
			}

			WriteFile(
				Path.Combine( PatchPackagePath, "layout.json" ),
				JsonSerializer.Serialize( PackageLayout, typeof( Layout ), SerializerOptions ).Replace( "\r\n", "\n" )
			);
		}

		static void GenerateManifest( string PatchPackagePath, Manifest OriginalPackageManifest, string OriginalPackageVersionRequirement )
		{
			Log( "Creating package manifest" );

			Manifest PatchPackageManifest = new Manifest
			{
				Title = "Aerosoft CRJ Cockpit Interaction Fix",
				ContentType = "CORE",
				PackageVersion = PatchPackageVersion,
				MinimumGameVersion = OriginalPackageManifest.MinimumGameVersion,
				Dependencies = {
					new Dependency {
						PackageName = OriginalPackageName,
						PackageVersion = OriginalPackageVersionRequirement
					}
				}
			};

			WriteFile(
				Path.Combine( PatchPackagePath, "manifest.json" ),
				JsonSerializer.Serialize( PatchPackageManifest, typeof( Manifest ), SerializerOptions )
			);
		}

		static void Log( string Message )
		{
			Console.WriteLine( Message );
		}

		static void LogError( string Message )
		{
			Console.WriteLine( $"Error: { Message }" );
		}

		static void CreateDirectory( string Path )
		{
			Log( $"Creating directory: '{ Path }'" );
			Directory.CreateDirectory( Path );
		}

		static void DeleteDirectory( string Path )
		{
			if ( Directory.Exists( Path ) )
			{
				Log( $"Deleting directory: '{ Path }'" );
				Directory.Delete( Path, true );
			}
		}

		static void WriteFile( string Path, string Text )
		{
			Log( $"Writing file: '{ Path }'" );
			File.WriteAllText( Path, Text );
		}

		static void WriteFile( string Path, string Text, Encoding Encoding )
		{
			Log( $"Writing file: '{ Path }'" );
			File.WriteAllText( Path, Text, Encoding );
		}

		static void CopyFile( string Source, string Destination )
		{
			Log( $"Copying file: From '{ Source }' to '{ Destination }'" );
			File.Copy( Source, Destination );
		}

		static string GetCommunityFolderLocationFromUserConfig( string UserConfigPath )
		{
			var UserConfigLines = File.ReadAllLines( UserConfigPath );
			foreach ( var Line in UserConfigLines )
			{
				if ( Line.StartsWith( "InstalledPackagesPath" ) )
				{
					return Line.Substring( Line.IndexOf( '"' ) ).Trim( '"' );
				}
			}

			LogError( "Failed to find 'InstalledPackagesPath' in user config" );
			return null;
		}

		static string GetPackagesPath()
		{
			var UserProfilePath = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
			if ( UserProfilePath.Length == 0 )
			{
				LogError( "Failed to resolve user profile path" );
				return null;
			}

			var UserCFGStorePath = Path.Combine( UserProfilePath, @"AppData\Local\Packages\Microsoft.FlightSimulator_8wekyb3d8bbwe\LocalCache\UserCfg.opt" );
			var UserCFGSteamPath = Path.Combine( UserProfilePath, @"AppData\Roaming\Microsoft Flight Simulator\UserCfg.opt" );

			if ( File.Exists( UserCFGStorePath ) )
			{
				return GetCommunityFolderLocationFromUserConfig( UserCFGStorePath );
			}
			else if ( File.Exists( UserCFGSteamPath ) )
			{
				return GetCommunityFolderLocationFromUserConfig( UserCFGSteamPath );
			}

			LogError( "Failed to resolve UserCfg.opt path" );
			return null;
		}

		static string GetPackagePath( PackageSource Source, string PackageName )
		{
			var PackagesPath = GetPackagesPath();
			if ( PackagesPath != null )
			{
				switch ( Source )
				{
					case PackageSource.Community:
						return Path.Combine( PackagesPath, @"Community", PackageName );
					case PackageSource.Official:
						return Path.Combine( PackagesPath, @"Official\OneStore", PackageName );
				}
			}

			return null;
		}

		static void WriteWelcomeMessage()
		{
			Console.Write( Resources.WelcomeMessage
				.Replace( "{OriginalPackageName}", OriginalPackageName )
				.Replace( "{PatchPackageName}", PatchPackageName )
				.Replace( "{OriginalPackageVersionRequirement_Community}", OriginalPackageVersionRequirement_Community )
				.Replace( "{OriginalPackageVersionRequirement_Marketplace}", OriginalPackageVersionRequirement_Marketplace )
			);
			Console.ReadKey();
			Console.WriteLine();
			Console.WriteLine();
		}

		static void WriteFailureMessage()
		{
			Console.WriteLine();
			Log( "Package generation failed" );
		}

		static void WriteSuccessMessage()
		{
			Console.WriteLine();
			Log( "Package generated successfully!" );
		}

		static void WaitForExit()
		{
			Console.Write( "Press any key to exit... " );
			Console.ReadKey();
			Console.WriteLine();
		}
	}
}
