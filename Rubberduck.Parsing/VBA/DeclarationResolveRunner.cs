﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rubberduck.VBEditor;

namespace Rubberduck.Parsing.VBA
{
    public class DeclarationResolveRunner : DeclarationResolveRunnerBase
    {
        private const int _maxDegreeOfDeclarationResolverParallelism = -1;

        public DeclarationResolveRunner(
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

            var options = new ParallelOptions();
            options.CancellationToken = token;
            options.MaxDegreeOfParallelism = _maxDegreeOfDeclarationResolverParallelism;
            try
            {
                Parallel.ForEach(modules,
                    options,
                    module =>
                    {
                        ResolveDeclarations(module,
                            _state.ParseTrees.Find(s => s.Key == module).Value,
                            token);
                    }
                );
            }
            catch (AggregateException exception)
            {
                if (exception.Flatten().InnerExceptions.All(ex => ex is OperationCanceledException))
                {
                    throw exception.InnerException ?? exception; //This eliminates the stack trace, but for the cancellation, this is irrelevant.
                }
                _parserStateManager.SetStatusAndFireStateChanged(this, ParserState.ResolverError, token);
                throw;
            }
        }
    }
}
