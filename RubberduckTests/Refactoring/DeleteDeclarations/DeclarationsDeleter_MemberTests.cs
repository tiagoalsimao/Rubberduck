﻿using Antlr4.Runtime;
using NUnit.Framework;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Rewriter;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Refactorings;
using Rubberduck.Refactorings.AddInterfaceImplementations;
using Rubberduck.Refactorings.DeleteDeclarations;
using Rubberduck.Refactorings.Exceptions;
using Rubberduck.Refactorings.ImplementInterface;
using Rubberduck.SmartIndenter;
using Rubberduck.VBEditor.SafeComWrappers;
using Rubberduck.VBEditor.SafeComWrappers.Abstract;
using RubberduckTests.Mocks;
using RubberduckTests.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RubberduckTests.Refactoring.DeleteDeclarations
{
    [TestFixture]
    public class DeclarationsDeleter_MemberTests
    {
        private readonly DeleteDeclarationsTestSupport _support = new DeleteDeclarationsTestSupport();

        [TestCase("Sub", "End Sub")]
        [TestCase("Property Let", "End Property")]
        [TestCase("Property Set", "End Property")]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void RemoveProcedureDeclarations(string methodType, string endStmt)
        {
            var inputCode =
$@"
Option Explicit

Public {methodType} Test1()
{endStmt}



Public {methodType} Test2()
{endStmt}


Public {methodType} Test3()
{endStmt}

Public {methodType} Test4()
{endStmt}
";

            var expected =
$@"
Option Explicit

Public {methodType} Test1()
{endStmt}



Public {methodType} Test4()
{endStmt}
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "Test2", "Test3"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [TestCase("Function", "End Function")]
        [TestCase("Property Get", "End Property")]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void RemoveFunctionDeclarations(string methodType, string endStmt)
        {
            var inputCode =
$@"
Option Explicit

Public {methodType} Test1() As Long
{endStmt}



Public {methodType} Test2() As Long
{endStmt}


Public {methodType} Test3() As Long
{endStmt}

Public {methodType} Test4() As Long
{endStmt}
";

            var expected =
$@"
Option Explicit

Public {methodType} Test1() As Long
{endStmt}



Public {methodType} Test4() As Long
{endStmt}
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "Test2", "Test3"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [TestCase("")]
        [TestCase("    ")]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void CommentHandling_RemovesSameLogicalLineOnly(string indent)
        {
            var inputCode =
$@"
Option Explicit

{indent}'Comment above Test1
Public Sub Test1()  'Comment on same logical line as Test1
End Sub

{indent}'Comment following Test1
";

            var expected =
$@"
Option Explicit

{indent}'Comment above Test1

{indent}'Comment following Test1
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "Test1"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [Test]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void DeleteAll()
        {
            var inputCode =
$@"
Option Explicit

Private mTest As Long

Public Sub Test1()  'Comment on same logical line as Test1
End Sub

Public Sub Test2()  'Comment on same logical line as Test2
End Sub

Public Sub Test3()  'Comment on same logical line as Test3
End Sub

'Comment at End of Module
";

            var expected =
$@"
Option Explicit

Private mTest As Long

'Comment at End of Module
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "Test1", "Test2", "Test3"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [TestCase("Private mValue As Long", "Sub")]
        [TestCase("Private mValue As Long", "Property Let")]
        [TestCase("Private mValue As Long", "Property Set")]
        [TestCase("\r\n", "Sub")]
        [TestCase("\r\n", "Property Let")]
        [TestCase("\r\n", "Property Set")]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void TargetHasDescriptionAttribute_RemovesAnnotation(string moduleSectionDeclaration, string memberTypeDeclaration)
        {
            var endMemberExpression = memberTypeDeclaration.Contains("Property")
                ? "End Property"
                : "End Sub";

            var inputCode =
$@"
Option Explicit

{moduleSectionDeclaration}

'@Description(""This {memberTypeDeclaration} is not used"")
Public {memberTypeDeclaration} ImNotUsed(ByVal arg As Long)
    ImUsed arg
{endMemberExpression}

'This {memberTypeDeclaration} gets used
Private {memberTypeDeclaration} ImUsed(ByVal arg As Long)
    mValue = arg
{endMemberExpression}
";
            var expected =
$@"
Option Explicit

{moduleSectionDeclaration}

'This {memberTypeDeclaration} gets used
Private {memberTypeDeclaration} ImUsed(ByVal arg As Long)
    mValue = arg
{endMemberExpression}
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "ImNotUsed"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [Test]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void TargetHasEnumeratorAndDescriptionAttributes_RemovesAllAnnotations()
        {
            var inputCode =
$@"
Option Explicit

Private InternalState As VBA.Collection

'@Enumerator
'@Description(""This is an enumerator too!"")
Public Property Get NewEnum() As IUnknown
    Set NewEnum = InternalState.[_NewEnum]
End Property

Private Sub Class_Initialize()
    Set InternalState = New VBA.Collection
End Sub
";
            var expected =
$@"
Option Explicit

Private InternalState As VBA.Collection

Private Sub Class_Initialize()
    Set InternalState = New VBA.Collection
End Sub
";
            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "NewEnum"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        //TODO: What is the correct result here?  Should the @DefaultMember annotation be moved to a 'surviving' property?
        [Test]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void TargetHasDefaultMemberPropertyAndDescription_RemovesAllAnnotations()
        {
            var inputCode =
$@"
Option Explicit
Private InternalState As VBA.Collection

'@DefaultMember
'@Description(""This is a DefaultMember too!"")
Public Property Get Item(ByVal Index As Variant) As Variant
    Attribute Item.VB_UserMemId = 0
    Item = InternalState(Index)
End Property
'if the default member is a property, only the Get accessor needs the attribute/annotation.
Public Property Let Item(ByVal Index As Variant, ByVal Value As Variant)
    InternalState(Index) = Value
End Property

Private Sub Class_Initialize()
    Set InternalState = New VBA.Collection
End Sub
";
            var expected =
$@"
Option Explicit
Private InternalState As VBA.Collection

'if the default member is a property, only the Get accessor needs the attribute/annotation.
Public Property Let Item(ByVal Index As Variant, ByVal Value As Variant)
    InternalState(Index) = Value
End Property

Private Sub Class_Initialize()
    Set InternalState = New VBA.Collection
End Sub
";
            IEnumerable<Declaration> TestTargets(RubberduckParserState st, string id)
                => st.DeclarationFinder.UserDeclarations(DeclarationType.PropertyGet);

            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => TestTargets(state, "Item"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }

        [Test]
        [Category("Refactorings")]
        [Category(nameof(DeleteDeclarationsRefactoringAction))]
        public void TargetHasIgnoreAttribute_RemovesAllAnnotations()
        {
            var inputCode =
$@"
Option Explicit

Sub IUseBizz()
    Bizz
End Sub

'@Ignore ProcedureNotUsed, UseMeaningfulName
Private Sub X()
    Bizz
End Sub

Private Sub Bizz()
End Sub
";
            var expected =
$@"
Option Explicit

Sub IUseBizz()
    Bizz
End Sub

Private Sub Bizz()
End Sub
";

            var actualCode = _support.GetRetainedCodeBlock(inputCode, state => _support.TestTargets(state, "X"));
            StringAssert.Contains(expected, actualCode);
            StringAssert.AreEqualIgnoringCase(expected, actualCode);
        }
    }
}
