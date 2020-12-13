﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SyntaxHighlighter
{
    internal interface IParser
    {
        string Parse(string text);
    }

    /// <summary>
    /// 簡易 RTF パーサー。
    /// ぶっちゃけ、Visual Studio のテキストエディターが出力する RTF 以外対応する気まるでなし。
    /// </summary>
    internal class RtfParser : IParser
    {
        #region public メソッド

        /// <summary>
        /// Visual Studio のソースエディターからコピった RTF 文章を、ufcpp で使ってる XML 形式に変換する。
        /// </summary>
        /// <param name="text">RTF 文章</param>
        /// <returns>XML 化したもの</returns>
        public string Parse(string text)
        {
            ReadHeader(text);
            return TagReplace(text);
        }

        #endregion
        #region 変換に使う設定

        public static IDictionary<Color, string> ColorToTagNameCsharp = new Dictionary<Color, string>
        {
            { new Color { R =   0, G =   0, B = 255}, "reserved" },
            { new Color { R = 128, G = 128, B = 128}, "inactive" },
            { new Color { R =   0, G = 128, B =   0}, "comment" },
            { new Color { R =  43, G = 145, B = 175}, "type" },
            { new Color { R = 163, G =  21, B =  21}, "string" },
            { new Color { R = 165, G =  42, B =  42}, "literal" },
        };

        public static IDictionary<Color, string> ColorToTagNameXml = new Dictionary<Color, string>
        {
            { new Color { R =   0, G =   0, B = 255}, "attvalue" },
            { new Color { R = 255, G =   0, B =   0}, "attribute" },
            { new Color { R = 128, G = 128, B = 128}, "inactive" },
            { new Color { R =   0, G = 128, B =   0}, "comment" },
            { new Color { R =  43, G = 145, B = 175}, "xsl" },
            { new Color { R = 163, G =  21, B =  21}, "element" },
        };

        public RtfParser(Mode mode)
        {
            if (mode == Mode.Csharp) ColorToTagNameMap = ColorToTagNameCsharp;
            else if (mode == Mode.Xml) ColorToTagNameMap = ColorToTagNameXml;
            else throw new InvalidOperationException();
        }

        /// <summary>
        /// 色と XML タグの対応表。
        /// </summary>
        private readonly IDictionary<Color, string> ColorToTagNameMap;

        // ↑シリアライズできるようにしといた方がいいかも。

        #endregion
        #region 処理本体
        #region ヘッダー

        /// <summary>
        /// RTF の {\colortbl ...} を抽出。
        /// </summary>
        private readonly Regex regColortbl = new Regex(@"\{\\colortbl\s*;(?<items>.*?)\}", RegexOptions.Compiled);

        /// <summary>
        /// RTF の {\colortbl ...} の中身から色情報を抽出。
        /// </summary>
        private readonly Regex regColortblItem = new Regex(@"\\red(?<r>\d*)\\green(?<g>\d*)\\blue(?<b>\d*)", RegexOptions.Compiled);

        /// <summary>
        /// RTF のヘッダー情報を読み込む。
        /// </summary>
        /// <param name="text">RTF</param>
        private void ReadHeader(string text)
        {
            var m = regColortbl.Match(text);
            var items = m.Groups["items"].Value;

            m = regColortblItem.Match(items);

            List<Color> colors = new List<Color>();
            for (int i = 0; m.Success; m = m.NextMatch(), ++i)
            {
                colors.Add(new Color
                {
                    R = byte.Parse(m.Groups["r"].Value),
                    G = byte.Parse(m.Groups["g"].Value),
                    B = byte.Parse(m.Groups["b"].Value),
                });
            }

            colorTable_ = new string[colors.Count + 1];
            for (int i = 0; i < colors.Count; i++)
            {
                if (ColorToTagNameMap.ContainsKey(colors[i]))
                    colorTable_[i + 1] = ColorToTagNameMap[colors[i]];
            }
        }

        #endregion
        #region 単純な置き換え用の正規表現

        /// <summary>
        /// VS のはきだす RTF は日本語が \input2\u***** ** みたいな変な状態になってるのでそれをもとに戻す。
        /// </summary>
        private readonly Regex regU = new Regex(@"\\uinput2\\u(?<code>-?\d*)\s..", RegexOptions.Compiled);

        private string DecodeU(Match m)
        {
            return new string((char)int.Parse(m.Groups["code"].Value), 1);
        }

        /// <summary>
        /// 改行文字のところが \par になってるので、それを抽出。
        /// </summary>
        private readonly Regex regPar = new Regex(@"(?<=[^\\])\\par\s?", RegexOptions.Compiled);

        /// <summary>
        /// 末尾の空白。
        /// </summary>
        private readonly Regex regTailWhite = new Regex(@"\s*$", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// 未対応のタグ削除用。
        /// </summary>
        private readonly Regex regOther = new Regex(@"\\\w+\s", RegexOptions.Singleline | RegexOptions.Compiled);

        #endregion
        #region RTF の \cf* → XML タグ

        /// <summary>
        /// RTF の \cf* の色番号をどの XML タグに変換するかの一覧。
        /// </summary>
        private string[]? colorTable_;

        private string GetTag(Match m)
        {
            var i = GetCfNumber(m);
            System.Diagnostics.Debug.Assert(colorTable_ is not null);
            var tag = colorTable_[i];
            return tag;
        }

        private static int GetCfNumber(Match m)
        {
            var i = int.Parse(m.Groups["n"].Value);
            return i;
        }

        #endregion
        #region 本文

        /// <summary>
        /// RTF のうち、文章本体を抽出。
        /// </summary>
        private readonly Regex regBody = new Regex(@"\\f0\s*\\fs\d*\s?(?<body>.*?)(?<=[^\\])\}", RegexOptions.Singleline | RegexOptions.Compiled);
        private string? prevTag_ = null;

        /// <summary>
        /// &lt; &gt; &amp; にしたり、
        /// \par を消したり、
        /// \cf* を XML タグに変換したり。
        /// </summary>
        /// <param name="text">変換元</param>
        /// <returns>変換結果</returns>
        private string TagReplace(string text)
        {
            text = regU.Replace(text, DecodeU);
            var body = regBody.Match(text).Groups["body"].Value;

            body = body.Replace("&", "&amp;");
            body = body.Replace("<", "&lt;");
            body = body.Replace(">", "&gt;");
            body = regPar.Replace(body, Environment.NewLine);
            body = body.Replace(@"\{", "{");
            body = body.Replace(@"\}", "}");

            body = regCf.Replace(body, InsertTag);
            body = regPairTag1.Replace(body, RemoveWhiteSpaceElement);
            body = regPairTag2.Replace(body, RemoveWhiteSpaceElement);
            body = regTailWhite.Replace(body, "");
            body = ReplaceUnicodeEscape(body);
            body = regOther.Replace(body, "");

            body = body.Replace(@"\\", @"\");

            return body;
        }

        #endregion
        #region Unicode Escape

        private static readonly Regex regUnicodeEscape = new Regex(@"\\uc1\\u(?<code>-?\d+)\?", RegexOptions.Singleline | RegexOptions.Compiled);

        private static string ReplaceUnicodeEscape(string s)
        {
            return regUnicodeEscape.Replace(s, ReplaceUnicodeEscape);
        }

        private static string ReplaceUnicodeEscape(Match m)
        {
            var code = int.Parse(m.Groups["code"].ToString());
            if (code < 0) code += 65536; // サロゲートペア文字の時、なぜかマイナスの値になってるっぽい。65536足したらちゃんとした Unicode (UTF16)になるみたい。
            return ((char)code).ToString();
        }

        #endregion
        #region 空白の除去

        /// <summary>
        /// <![CDATA[
        /// <tag>   </tag>
        /// ]]>
        /// みたいな構造を抽出。
        /// </summary>
        private readonly Regex regPairTag1 = new Regex(@"<(?<open>[^>]*?)>(?<content>\s*?)</(?<close>[^>]*?)>", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// <![CDATA[
        /// </tag>   <tag>
        /// ]]>
        /// みたいな構造を抽出。
        /// </summary>
        private readonly Regex regPairTag2 = new Regex(@"</(?<close>[^>]*?)>(?<content>\s*?)<(?<open>[^>]*?)>", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// 中に空白文字しかないタグを消す。
        /// </summary>
        /// <param name="m">マッチ結果</param>
        /// <returns>変換結果</returns>
        private string RemoveWhiteSpaceElement(Match m)
        {
            if (m.Groups["open"].Value == m.Groups["close"].Value)
                return m.Groups["content"].Value;
            else
                return m.Value;
        }

        #endregion
        #region \cf → XML タグ

        /// <summary>
        /// RTF の \cf* を抽出。
        /// </summary>
        private static readonly Regex regCf = new Regex(@"(^|(?<=[^\\]))\\cf(?<n>\d+)? ", RegexOptions.Singleline | RegexOptions.Compiled);



        /// <summary>
        /// RTF の \cf* を XML タグに置き換える。
        /// </summary>
        /// <param name="m">マッチ結果</param>
        /// <returns>変換結果</returns>
        private string InsertTag(Match m)
        {
            var tag = GetTag(m);

            string ret;

            if (string.IsNullOrEmpty(prevTag_))
                if (string.IsNullOrEmpty(tag))
                    ret = string.Empty;
                else
                    ret = FormatTag(tag);
            else
                if (string.IsNullOrEmpty(tag))
                    ret = "</span>";
                else
                    ret = "</span>" + FormatTag(tag);

            prevTag_ = tag;
            return ret;
        }

        private static string FormatTag(string tag) => string.Format("<span class=\"{0}\">", tag);

        #endregion
        #endregion
    }
}
