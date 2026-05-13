using System.Text.RegularExpressions;
namespace JulesClient.Services;

public partial class DiffParser {
    [GeneratedRegex(@"^diff --git a/(?<old>.+) b/(?<new>.+)$")] private static partial Regex DiffHeader();
    [GeneratedRegex(@"^@@ -(?<os>\d+)(?:,(?<oc>\d+))? \+(?<ns>\d+)(?:,(?<nc>\d+))? @@")] private static partial Regex HunkHeader();
    public static ParsedPatch Parse(string patch) {
        var res=new ParsedPatch{Files=new()}; if(string.IsNullOrWhiteSpace(patch))return res;
        ParsedFile? cf=null; ParsedHunk? ch=null; int ol=0,nl=0;
        foreach(var rl in patch.Split('\n')) {
            var l=rl.TrimEnd('\r'); var dm=DiffHeader().Match(l);
            if(dm.Success){cf=new(){OldPath=dm.Groups["old"].Value,NewPath=dm.Groups["new"].Value,Hunks=new()}; res.Files.Add(cf); ch=null; continue;}
            if(cf==null)continue; var hm=HunkHeader().Match(l);
            if(hm.Success){ol=int.Parse(hm.Groups["os"].Value); nl=int.Parse(hm.Groups["ns"].Value); ch=new(){Header=l,Lines=new()}; cf.Hunks.Add(ch); continue;}
            if(ch==null)continue;
            var dl=l switch {
                var x when x.StartsWith("+")=>new ParsedLine{Type=DiffLineType.Added,Content=l[1..],OldLineNumber=null,NewLineNumber=nl++},
                var x when x.StartsWith("-")=>new ParsedLine{Type=DiffLineType.Removed,Content=l[1..],OldLineNumber=ol++,NewLineNumber=null},
                var x when x.StartsWith(" ")||l==""=>new ParsedLine{Type=DiffLineType.Context,Content=l.Length>0?l[1..]:"",OldLineNumber=ol++,NewLineNumber=nl++},
                var x when x.StartsWith(@"\")=>new ParsedLine{Type=DiffLineType.Metadata,Content=l,OldLineNumber=null,NewLineNumber=null},
                _=>new ParsedLine{Type=DiffLineType.Unknown,Content=l,OldLineNumber=null,NewLineNumber=null}
            }; ch.Lines.Add(dl);
        } return res;
    }
}
public record ParsedPatch{public List<ParsedFile> Files{get;init;}=new();}
public record ParsedFile{public string OldPath{get;init;}=""; public string NewPath{get;init;}=""; public List<ParsedHunk> Hunks{get;init;}=new();}
public record ParsedHunk{public string Header{get;init;}=""; public List<ParsedLine> Lines{get;init;}=new();}
public record ParsedLine{public DiffLineType Type{get;init;} public string Content{get;init;}=""; public int? OldLineNumber{get;init;} public int? NewLineNumber{get;init;}}
public enum DiffLineType{Added,Removed,Context,Metadata,Unknown}
