using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace GfxQA.ShaderVariantTool
{
    
    //===================================================================================================

    public class ShaderProgram
    {
        public string gfxAPI = "";
        public uint count_internal = 0;
        public uint count_unique = 0;

        public ShaderProgram(string api, uint program_internal, uint program_unique)
        {
            gfxAPI = api;
            count_internal = program_internal;
            count_unique = program_unique;
        }
    }

    public class ShaderItem
    {
        public bool isComputeShader = false;
        public string name;
        public string assetPath = "";
        
        public uint count_variant_before = 0; //includes dynamic variants
        public uint count_dynamicVariant_before = 0;
        public uint count_variant_after = 0; //includes dynamic variants
        public uint count_dynamicVariant_after = 0;

        public UInt64 editorLog_variantOriginalCount = 0;
        public UInt64 editorLog_variantAfterPrefilteringCount = 0;
        public UInt64 editorLog_variantAfterBuiltinStrippingCount = 0;
        public UInt64 editorLog_variantAfterSciptableStrippingCount = 0;
        public UInt64 editorLog_variantMeshDataOptimizationCached = 0;
        public UInt64 editorLog_variantMeshDataOptimizationCompiled = 0;
        public uint editorLog_variantCompiledCount = 0;
        public uint editorLog_variantInCache = 0;
        public float editorLog_timeCompile = 0;
        public float editorLog_timeStripping = 0;
        
        public List<ShaderProgram> programs = new List<ShaderProgram>();

        public int FindMatchingProgramItem(ShaderProgram pgm)
        {
            //find the matching shader program
            int matchedId = programs.FindIndex
            ( e =>
                e.gfxAPI == pgm.gfxAPI
            );

            return matchedId;
        }

        public List<KeywordItem> keywordItems = new List<KeywordItem>();
        public List<string> keywordItemsLookupString = new List<string>();
        
        private string GetKeywordItemsLookupString(KeywordItem scv)
        {
            return String.Concat(scv.shaderName, scv.passName, scv.passType, scv.shaderType, scv.kernelName, scv.graphicsTier, scv.buildTarget, scv.shaderCompilerPlatform, scv.platformKeywords, scv.shaderKeywordName, scv.shaderKeywordType);
        }

        public int FindMatchingVariantItem(KeywordItem scv)
        {
            int matchedId = keywordItemsLookupString.IndexOf(GetKeywordItemsLookupString(scv));
            return matchedId;
        }

        //Use in Preprocess_Before
        public void SetMatchedKeywordItem(KeywordItem scv)
        {
            int matchedID = FindMatchingVariantItem(scv);
            if(matchedID == -1)
            {
                scv.appearCount_before++;
                keywordItems.Add(scv);
                keywordItemsLookupString.Add(GetKeywordItemsLookupString(scv));
            }
            else
            {
                keywordItems[matchedID].appearCount_before ++;
            }
        }
        
        //Use in Preprocess_After
        public void SetMatchedKeywordItemCount(KeywordItem scv)
        {
            int matchedID = FindMatchingVariantItem(scv);
            keywordItems[matchedID].appearCount_after ++;
        }
        
        //Use in Build Postprocessor
        public void SetKeywordDeclareType()
        {
            if(isComputeShader)
            {
                //Not able to open this
                if(assetPath.EndsWith("unity_builtin_extra"))
                {
                    foreach(KeywordItem item in keywordItems)
                    {
                        item.shaderKeywordDeclareType = "N/A for builtin resource";
                    }
                    return;
                }

                //A list of keyword and type mapping
                Dictionary<string, string> keywordDeclareType = new Dictionary<string, string>();

                //Read shader code and find the #pragma lines
                string shaderCode = System.IO.File.ReadAllText(assetPath);
                GetDirectPragmaKeywordType(shaderCode, ref keywordDeclareType);

                //Read #include_with_pragmas lines as declare types are in seperate hlsl
                string pattern = @"#include_with_pragmas\s""(.*)""";
                MatchCollection matches = Regex.Matches(shaderCode, pattern);
                List<Match> matchesList = matches.ToList();
                foreach(Match m in matchesList)
                {
                    //Read hlsl code and find the #pragma lines
                    string hlslPath = m.Groups[1].Value;
                    string hlslCode = System.IO.File.ReadAllText(hlslPath);
                    GetDirectPragmaKeywordType(hlslCode, ref keywordDeclareType);
                }

                //Match the mapping with keywords
                foreach(KeywordItem item in keywordItems)
                {
                    string keyword = item.shaderKeywordName;
                    item.shaderKeywordDeclareType = keywordDeclareType.FirstOrDefault(x => x.Key.Contains(keyword)).Value;
                }
            }
            else
            {
                //A list of keyword and type mapping
                Dictionary<string, string> keywordDeclareType = new Dictionary<string, string>();
            
                //Get the shader data
                var shader = Shader.Find(name);
                var shaderData = ShaderUtil.GetShaderData(shader);
                var subShader = shaderData.GetSubshader(0);
                for (int i = 0; i < subShader.PassCount; i++)
                {
                    //Get source code
                    var pass = subShader.GetPass(i);
                    string shaderCode = pass.SourceCode;
                
                    //Read shader code and find the #pragma lines
                    GetDirectPragmaKeywordType(shaderCode, ref keywordDeclareType);

                    //Read #include_with_pragmas lines as declare types are in seperate hlsl
                    string pattern = @"#include_with_pragmas\s""(.*)""";
                    MatchCollection matches = Regex.Matches(shaderCode, pattern);
                    List<Match> matchesList = matches.ToList();
                    foreach(Match m in matchesList)
                    {
                        //Read hlsl code and find the #pragma lines
                        string hlslPath = m.Groups[1].Value;
                        string hlslCode = System.IO.File.ReadAllText(hlslPath);
                        GetDirectPragmaKeywordType(hlslCode, ref keywordDeclareType);
                    }
                }
            
                //Match the mapping with keywords
                foreach(KeywordItem item in keywordItems)
                {
                    string keyword = item.shaderKeywordName;
                    item.shaderKeywordDeclareType = keywordDeclareType.FirstOrDefault(x => x.Key.Contains(keyword)).Value;
                }
            }
        }

        private void GetDirectPragmaKeywordType(string shaderCode, ref Dictionary<string, string> keywordDeclareType)
        {
            //Read shader code and find the #pragma lines
            string pattern = @"#pragma\s(?!\btarget\b)(\w+)\s(\w.*)";
            MatchCollection matches = Regex.Matches(shaderCode, pattern);
            List<Match> matchesList = matches.ToList();
            foreach(Match m in matchesList)
            {
                string key = m.Groups[2].Value;
                if(!keywordDeclareType.ContainsKey(key))
                {
                    keywordDeclareType.Add(key, m.Groups[1].Value);
                }
            }
        }
    }

    //===================================================================================================

    public class KeywordItem
    {
        //shader
        public string shaderName;

        //snippet
        public string passType = "--";
        public string passName = "--";
        public string shaderType = "--";

        //compute kernel
        public string kernelName = "--";

        //data
        public string graphicsTier = "--";
        public string buildTarget = "--"; 
        public string shaderCompilerPlatform = "--";

        //data - PlatformKeywordSet
        public string platformKeywords = "--";

        //data - ShaderKeywordSet
        public string shaderKeywordName = "No Keyword / All Off";
        public string shaderKeywordType = "--";
        public string shaderKeywordDeclareType = "--";
        public bool isDynamic = false;

        //how many times this variant appears in IPreprocessShaders
        public int appearCount_before = 0;
        public int appearCount_after = 0;

        //Constructor for normal shader
        public KeywordItem(Shader shader, ShaderSnippetData snippet, ShaderCompilerData data, ShaderKeyword sk, bool defaultVariant)
        {
            shaderName = shader.name;
            passName = snippet.passName;
            passType = snippet.passType.ToString();
            shaderType = snippet.shaderType.ToString();
            graphicsTier = data.graphicsTier.ToString();
            buildTarget = data.buildTarget.ToString();
            shaderCompilerPlatform = data.shaderCompilerPlatform.ToString();
            platformKeywords = Helper.GetPlatformKeywordList(data.platformKeywordSet);

            if(!defaultVariant)
            {
                //shaderKeywordName
                LocalKeyword lkey = new LocalKeyword(shader,sk.name);
                isDynamic = lkey.isDynamic;
                shaderKeywordName = sk.name;
                
                //shaderKeywordType
                shaderKeywordType = ShaderKeyword.GetGlobalKeywordType(sk).ToString(); //""+sk[k].GetKeywordType().ToString();
                
                //Bug checking
                if( !sk.IsValid() )
                {
                    Debug.LogError(String.Concat("ShaderVariantTool error #E06. Shader ",shaderName," Keyword ",shaderKeywordName," is invalid."));
                }
                if( !data.shaderKeywordSet.IsEnabled(sk) )
                {
                    Debug.LogWarning(String.Concat("ShaderVariantTool error #E07. Shader ",shaderName," Keyword ",shaderKeywordName," is not enabled. You can create a custom shader stripping script to strip it."));
                }
            }
        }

        //Constructor for compute shader
        public KeywordItem(ComputeShader shader, string kernelname, ShaderCompilerData data, ShaderKeyword sk, bool defaultVariant)
        {
            shaderName = shader.name;
            kernelName = kernelname;
            graphicsTier = data.graphicsTier.ToString();
            buildTarget = data.buildTarget.ToString();
            shaderCompilerPlatform = data.shaderCompilerPlatform.ToString();
            platformKeywords = Helper.GetPlatformKeywordList(data.platformKeywordSet);

            if(!defaultVariant)
            {
                //shaderKeywordName
                LocalKeyword lkey = new LocalKeyword(shader,sk.name);
                isDynamic = lkey.isDynamic;
                shaderKeywordName = sk.name;
                
                //shaderKeywordType
                shaderKeywordType = ShaderKeyword.GetGlobalKeywordType(sk).ToString(); //""+sk[k].GetKeywordType().ToString();
                
                //Bug checking
                if( !sk.IsValid() )
                {
                    Debug.LogError(String.Concat("ShaderVariantTool error #E04. Shader ",shaderName," Keyword ",shaderKeywordName," is invalid."));
                }
                if( !data.shaderKeywordSet.IsEnabled(sk) )
                {
                    Debug.LogWarning(String.Concat("ShaderVariantTool error #E05. Shader ",shaderName," Keyword ",shaderKeywordName," is not enabled. You can create a custom shader stripping script to strip it."));
                }
            }
        }
    };
}
