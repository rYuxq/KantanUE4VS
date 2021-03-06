﻿// Copyright 2018 Cameron Angus. All Rights Reserved.

using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KUE4VS
{
    public class AddTypeTask : AddCodeElementTask
    {
        public SourceRelativeLocation Location { get; set; }
        public bool bPrivateHeader { get; set; }
        public bool Export{ get; set; }

        AddableTypeVariant _variant;
        public AddableTypeVariant Variant
        {
            get { return _variant; }
            set
            {
                SetProperty(ref _variant, value);
            }
        }

        UE4ClassDefnBase _base;
        public UE4ClassDefnBase Base
        {
            get { return _base; }
            set
            {
                SetProperty(ref _base, value);
            }
        }

        // Valid only for non-reflected types
        public string Namespace { get; set; }

        public AddTypeTask()
        {
            Location = new SourceRelativeLocation();
            ExtContext.Instance.RefreshModules();
            Location.Module = ExtContext.Instance.AvailableModules.FirstOrDefault();//Utils.GetDefaultModule();

            Export = false;

            if (Variant == AddableTypeVariant.UClass)
            {
                Base = EngineTypes.UClasses.FirstOrDefault();
            }
        }

        public override IEnumerable<GenericFileAdditionTask> GenerateAdditions(Project proj)
        {
            List<GenericFileAdditionTask> additions = new List<GenericFileAdditionTask>();

            string file_title = ElementName;
            string file_header = ExtContext.Instance.ExtensionOptions.SourceFileHeaderText;
            string type_keyword = Constants.TypeKeywords[Variant];
            bool is_reflected = Constants.ReflectedTypes[Variant];
            // @NOTE: Weirdly, passing null seems to crash the template processer
            string base_class = ReferenceEquals(Base, null) ? String.Empty : Base.Name;
            //String.IsNullOrEmpty(Base) ? String.Empty : Base;
            bool has_base = !String.IsNullOrEmpty(base_class);
            string unprefixed_type_name = ElementName;
            string type_name = Utils.GetPrefixedTypeName(unprefixed_type_name, base_class, Variant);
            bool should_export = Export;

            List<string> nspace = Utils.SplitNamespaceDefinition(Namespace);

            // @TODO: Perhaps have a 'header only' property, which defaults differently depending on Variant
            bool requires_cpp = (Variant == AddableTypeVariant.UClass || Variant == AddableTypeVariant.RawClass);
            // Cpp file
            if (requires_cpp)
            {
                // Generate content

                List<string> default_includes = new List<string>{
                };

                string cpp_contents = CodeGeneration.SourceGenerator.GenerateTypeCpp(
                    file_title,
                    file_header,
                    default_includes,
                    nspace,
                    type_name
                    );
                if (cpp_contents == null)
                {
                    return null;
                }

                var cpp_folder_path = Utils.GenerateSourceSubfolderPath(
                    Location.Module,
                    ModuleFileLocationType.Private,
                    String.IsNullOrWhiteSpace(Location.RelativePath) ? "" : Location.RelativePath
                    );

                additions.Add(new GenericFileAdditionTask
                {
                    FileTitle = file_title,
                    Extension = ".cpp",
                    FolderPath = cpp_folder_path,
                    Contents = cpp_contents
                });
            }

            // Header file
            {
                // Generate content

                List<string> default_includes = new List<string>();
                if (Variant == AddableTypeVariant.UInterface)
                {
                    default_includes.Add("UObject/Interface.h");
                }
                else if (has_base)
                {
                    if (!String.IsNullOrEmpty(Base.IncludePath))
                    {
                        default_includes.Add(Base.IncludePath);
                    }
                    else
                    {
                        // @TODO: Disallow? Warning?
                    }

                    if (Variant == AddableTypeVariant.UClass)
                    {
                        default_includes.Add("UObject/ScriptMacros.h");
                    }
                }
                else
                {
                    default_includes.Add("CoreMinimal.h");
                    if (Constants.ReflectedTypes[Variant])
                    {
                        default_includes.Add("UObject/ObjectMacros.h");
                    }
                }

                string hdr_contents = null;
                switch (Variant)
                {
                    case AddableTypeVariant.UClass:
                    case AddableTypeVariant.UStruct:
                    case AddableTypeVariant.RawClass:
                    case AddableTypeVariant.RawStruct:
                        hdr_contents = CodeGeneration.SourceGenerator.GenerateTypeHeader(
                            file_title,
                            file_header,
                            default_includes,
                            type_keyword,
                            is_reflected,
                            nspace,
                            type_name,
                            base_class,
                            Location.Module.Name,
                            should_export
                            );
                        break;

                    case AddableTypeVariant.UInterface:
                        hdr_contents = CodeGeneration.SourceGenerator.GenerateUInterfaceHeader(
                            file_title,
                            file_header,
                            default_includes,
                            unprefixed_type_name,
                            Location.Module.Name,
                            should_export
                            );
                        break;

                    //case AddableTypeVariant.UEnum:
                        // @TODO:
                     //   break;
                }
                if (hdr_contents == null)
                {
                    return null;
                }

                // Now generate the paths where we want to add the files
                var hdr_folder_path = Utils.GenerateSourceSubfolderPath(
                    Location.Module,
                    bPrivateHeader ? ModuleFileLocationType.Private : ModuleFileLocationType.Public,
                    String.IsNullOrWhiteSpace(Location.RelativePath) ? "" : Location.RelativePath
                    );

                additions.Add(new GenericFileAdditionTask
                {
                    FileTitle = file_title,
                    Extension = ".h",
                    FolderPath = hdr_folder_path,
                    Contents = hdr_contents
                });
            }

            return additions;
        }
    }
}
