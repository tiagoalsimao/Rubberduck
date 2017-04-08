﻿using Rubberduck.VBEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rubberduck.Parsing.VBA
{
    public class SynchronousDeclarationResolveRunner : DeclarationResolveRunnerBase
    {
        public SynchronousDeclarationResolveRunner(
            RubberduckParserState state,
            IParserStateManager parserStateManager,
            ICOMReferenceManager comReferenceManager) 
        :base(
             state, 
             parserStateManager, 
             comReferenceManager)
        { }

        public override void ResolveDeclarations(ICollection<QualifiedModuleName> modules, CancellationToken token)
        {
            if (!modules.Any())
            {
                return;
            }

            _parserStateManager.SetModuleStates(modules, ParserState.ResolvingDeclarations, token);

            token.ThrowIfCancellationRequested();

            _projectDeclarations.Clear();

            token.ThrowIfCancellationRequested();

            try
            {
                foreach(var module in modules)
                {
                    ResolveDeclarations(module, _state.ParseTrees.Find(s => s.Key == module).Value, token);
                }
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                _parserStateManager.SetStatusAndFireStateChanged(this, ParserState.ResolverError, token);
                throw;
            }
        }
    }
}
