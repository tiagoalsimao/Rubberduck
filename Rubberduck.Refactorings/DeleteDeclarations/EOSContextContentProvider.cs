﻿using Antlr4.Runtime;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Rewriter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rubberduck.Refactorings.DeleteDeclarations
{
    internal class EOSContextContentProvider
    {
        protected static string _lineContinuationExpression = $"{Tokens.LineContinuation}{Environment.NewLine}";

        private readonly VBAParser.EndOfStatementContext _eosContext;
        private readonly IModuleRewriter _rewriter;
        private readonly List<VBAParser.IndividualNonEOFEndOfStatementContext> _individualNonEOFEndOfStatementContexts;

        public EOSContextContentProvider(VBAParser.EndOfStatementContext eosContext, IModuleRewriter rewriter, IEnumerable<VBAParser.AnnotationContext> deletedAnnotationContexts = null)
        {
            if (rewriter is null)
            {
                throw new ArgumentNullException();
            }

            _eosContext = eosContext;
            _rewriter = rewriter;
            _individualNonEOFEndOfStatementContexts = _eosContext != null
                ? _eosContext.individualNonEOFEndOfStatement().ToList()
                : new List<VBAParser.IndividualNonEOFEndOfStatementContext>();
        }

        public VBAParser.EndOfStatementContext EOSContext => _eosContext;

        public VBAParser.IndividualNonEOFEndOfStatementContext DeclarationLogicalLineCommentContext
            => _individualNonEOFEndOfStatementContexts.Any() && IsComment(_individualNonEOFEndOfStatementContexts.First())
                    ? _individualNonEOFEndOfStatementContexts.First()
                    : null;

        public bool HasDeclarationLogicalLineComment => DeclarationLogicalLineCommentContext != null;

        public string OriginalEOSContent => _eosContext?.GetText() ?? string.Empty;

        public string ModifiedEOSContent =>_eosContext != null
            ? _rewriter.GetText(_eosContext.Start.TokenIndex, _eosContext.Stop.TokenIndex) ?? OriginalEOSContent
            : string.Empty;

        public bool ModifiedContentContainsCommentMarker => ModifiedEOSContent.IndexOf(Tokens.CommentMarker) >= 0;

        public string ContentPriorToSeparationAndIndentation 
        {
            get
            {
                return ModifiedEOSContent.Length > 0
                    ? ModifiedEOSContent.Substring(0, ModifiedEOSContent.Length - SeparationAndIndentation.Length)
                    : string.Empty;
            }
        }
        
        public string ContentFreeOfStartingNewLines
            => string.Concat(ModifiedEOSContent.SkipWhile(c => IsNewLineCharacter(c)));

        private string SeparationAndIndentation => ModifiedEOSContent.StartsWith(":")
            ? string.Empty
            : Regex.Match(ModifiedEOSContent, @"(\r\n)+\s*$").Value;


        public string Indentation => GetSeparationAndIndentation.Indentation;

        public string Separation => GetSeparationAndIndentation.Separation;

        private (string Separation, string Indentation) GetSeparationAndIndentation
        {
            get
            {
                var endingNewLinesAndIndentation = SeparationAndIndentation;

                var indentation = string.Concat(endingNewLinesAndIndentation.SkipWhile(c => IsNewLineCharacter(c)));
                var separation = string.Concat(endingNewLinesAndIndentation.TakeWhile(c => c != ' '));
                return (separation, indentation);
            }
        }

        private bool IsComment(VBAParser.IndividualNonEOFEndOfStatementContext ctxt)
            => ctxt?.GetDescendent<VBAParser.CommentContext>() != null;

        private bool IsAnnotation(VBAParser.IndividualNonEOFEndOfStatementContext ctxt)
            => ctxt?.GetDescendent<VBAParser.AnnotationContext>() != null;

        private static bool IsNewLineCharacter(char c)
            => c == '\r' || c == '\n';
    }
}
