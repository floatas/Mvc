// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNet.Mvc.Razor.Precompilation;
using Microsoft.Dnx.Runtime;
using Moq;
using Xunit;

namespace Microsoft.AspNet.Mvc.Razor.Compilation
{
    public class CompilerCacheTest
    {
        private const string ViewPath = "Views/Home/Index.cshtml";
        private const string PrecompiledViewsPath = "Views/Home/Precompiled.cshtml";
        private readonly IDictionary<string, Type> _precompiledViews = new Dictionary<string, Type>
        {
            { PrecompiledViewsPath, typeof(PreCompile) }
        };

        public static TheoryData ViewImportsPaths =>
            new TheoryData<string>
            {
                Path.Combine("Views", "Home", "_ViewImports.cshtml"),
                Path.Combine("Views", "_ViewImports.cshtml"),
                "_ViewImports.cshtml",
            };

        [Fact]
        public void GetOrAdd_ReturnsFileNotFoundResult_IfFileIsNotFoundInFileSystem()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            var cache = new CompilerCache(fileProvider);

            // Act
            var result = cache.GetOrAdd("/some/path", ThrowsIfCalled);

            // Assert
            Assert.Same(CompilerCacheResult.FileNotFound, result);
            Assert.Null(result.CompilationResult);
        }

        [Fact]
        public void GetOrAdd_ReturnsCompilationResultFromFactory()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider);
            var type = typeof(TestView);
            var expected = UncachedCompilationResult.Successful(type, "hello world");

            // Act
            var result = cache.GetOrAdd(ViewPath, _ => expected);

            // Assert
            Assert.NotSame(CompilerCacheResult.FileNotFound, result);
            var actual = Assert.IsType<UncachedCompilationResult>(result.CompilationResult);
            Assert.NotNull(actual);
            Assert.Same(expected, actual);
            Assert.Equal("hello world", actual.CompiledContent);
            Assert.Same(type, actual.CompiledType);
        }

        [Fact]
        public void GetOrAdd_ReturnsFileNotFoundIfFileWasDeleted()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider);
            var type = typeof(TestView);
            var expected = UncachedCompilationResult.Successful(type, "hello world");

            // Act 1
            var result1 = cache.GetOrAdd(ViewPath, _ => expected);

            // Assert 1
            Assert.NotSame(CompilerCacheResult.FileNotFound, result1);
            Assert.Same(expected, result1.CompilationResult);

            // Act 2
            // Delete the file from the file system and set it's expiration trigger.
            fileProvider.DeleteFile(ViewPath);
            fileProvider.GetTrigger(ViewPath).IsExpired = true;
            var result2 = cache.GetOrAdd(ViewPath, ThrowsIfCalled);

            // Assert 2
            Assert.Same(CompilerCacheResult.FileNotFound, result2);
            Assert.Null(result2.CompilationResult);
        }

        [Fact]
        public void GetOrAdd_ReturnsNewResultIfFileWasModified()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider);
            var expected1 = UncachedCompilationResult.Successful(typeof(TestView), "hello world");
            var expected2 = UncachedCompilationResult.Successful(typeof(DifferentView), "different content");

            // Act 1
            var result1 = cache.GetOrAdd(ViewPath, _ => expected1);

            // Assert 1
            Assert.NotSame(CompilerCacheResult.FileNotFound, result1);
            Assert.Same(expected1, result1.CompilationResult);

            // Act 2
            // Verify we're getting cached results.
            var result2 = cache.GetOrAdd(ViewPath, ThrowsIfCalled);

            // Assert 2
            Assert.NotSame(CompilerCacheResult.FileNotFound, result2);
            Assert.Same(expected1.CompiledType, result2.CompilationResult.CompiledType);

            // Act 3
            fileProvider.GetTrigger(ViewPath).IsExpired = true;
            var result3 = cache.GetOrAdd(ViewPath, _ => expected2);

            // Assert 3
            Assert.NotSame(CompilerCacheResult.FileNotFound, result3);
            Assert.Same(expected2, result3.CompilationResult);
        }

        [Theory]
        [MemberData(nameof(ViewImportsPaths))]
        public void GetOrAdd_ReturnsNewResult_IfAncestorViewImportsWereModified(string globalImportPath)
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider);
            var expected1 = UncachedCompilationResult.Successful(typeof(TestView), "hello world");
            var expected2 = UncachedCompilationResult.Successful(typeof(DifferentView), "different content");

            // Act 1
            var result1 = cache.GetOrAdd(ViewPath, _ => expected1);

            // Assert 1
            Assert.NotSame(CompilerCacheResult.FileNotFound, result1);
            Assert.Same(expected1, result1.CompilationResult);

            // Act 2
            // Verify we're getting cached results.
            var result2 = cache.GetOrAdd(ViewPath, ThrowsIfCalled);

            // Assert 2
            Assert.NotSame(CompilerCacheResult.FileNotFound, result2);
            Assert.Same(expected1.CompiledType, result2.CompilationResult.CompiledType);

            // Act 3
            fileProvider.GetTrigger(globalImportPath).IsExpired = true;
            var result3 = cache.GetOrAdd(ViewPath, _ => expected2);

            // Assert 2
            Assert.NotSame(CompilerCacheResult.FileNotFound, result3);
            Assert.Same(expected2, result3.CompilationResult);
        }

        [Fact]
        public void GetOrAdd_DoesNotQueryFileSystem_IfCachedFileTriggerWasNotSet()
        {
            // Arrange
            var mockFileProvider = new Mock<TestFileProvider> { CallBase = true };
            var fileProvider = mockFileProvider.Object;
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider);
            var type = typeof(TestView);
            var expected = UncachedCompilationResult.Successful(type, "hello world");

            // Act 1
            var result1 = cache.GetOrAdd(ViewPath, _ => expected);

            // Assert 1
            Assert.NotSame(CompilerCacheResult.FileNotFound, result1);
            Assert.Same(expected, result1.CompilationResult);

            // Act 2
            var result2 = cache.GetOrAdd(ViewPath, ThrowsIfCalled);

            // Assert 2
            Assert.NotSame(CompilerCacheResult.FileNotFound, result2);
            Assert.IsType<CompilationResult>(result2.CompilationResult);
            Assert.Same(type, result2.CompilationResult.CompiledType);
            mockFileProvider.Verify(v => v.GetFileInfo(ViewPath), Times.Once());
        }

        [Fact]
        public void GetOrAdd_UsesViewsSpecifiedFromRazorFileInfoCollection()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            var cache = new CompilerCache(fileProvider, _precompiledViews);

            // Act
            var result = cache.GetOrAdd(PrecompiledViewsPath, ThrowsIfCalled);

            // Assert
            Assert.NotSame(CompilerCacheResult.FileNotFound, result);
            Assert.Same(typeof(PreCompile), result.CompilationResult.CompiledType);
        }

        [Fact]
        public void GetOrAdd_DoesNotRecompile_IfFileTriggerWasSetForPrecompiledFile()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            var cache = new CompilerCache(fileProvider, _precompiledViews);

            // Act
            fileProvider.Watch(PrecompiledViewsPath);
            fileProvider.GetTrigger(PrecompiledViewsPath).IsExpired = true;
            var result = cache.GetOrAdd(PrecompiledViewsPath, ThrowsIfCalled);

            // Assert
            Assert.NotSame(CompilerCacheResult.FileNotFound, result);
            Assert.Same(typeof(PreCompile), result.CompilationResult.CompiledType);
        }

        [Theory]
        [MemberData(nameof(ViewImportsPaths))]
        public void GetOrAdd_DoesNotRecompile_IfFileTriggerWasSetForViewImports(string globalImportPath)
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            var cache = new CompilerCache(fileProvider, _precompiledViews);

            // Act
            fileProvider.Watch(globalImportPath);
            fileProvider.GetTrigger(globalImportPath).IsExpired = true;
            var result = cache.GetOrAdd(PrecompiledViewsPath, ThrowsIfCalled);

            // Assert
            Assert.NotSame(CompilerCacheResult.FileNotFound, result);
            Assert.Same(typeof(PreCompile), result.CompilationResult.CompiledType);
        }

        [Fact]
        public void GetOrAdd_ReturnsRuntimeCompiledAndPrecompiledViews()
        {
            // Arrange
            var fileProvider = new TestFileProvider();
            fileProvider.AddFile(ViewPath, "some content");
            var cache = new CompilerCache(fileProvider, _precompiledViews);
            var expected = CompilationResult.Successful(typeof(TestView));

            // Act 1
            var result1 = cache.GetOrAdd(ViewPath, _ => expected);

            // Assert 1
            Assert.Same(expected, result1.CompilationResult);

            // Act 2
            var result2 = cache.GetOrAdd(ViewPath, ThrowsIfCalled);

            // Assert 2
            Assert.NotSame(CompilerCacheResult.FileNotFound, result2);
            Assert.Same(typeof(TestView), result2.CompilationResult.CompiledType);

            // Act 3
            var result3 = cache.GetOrAdd(PrecompiledViewsPath, ThrowsIfCalled);

            // Assert 3
            Assert.NotSame(CompilerCacheResult.FileNotFound, result2);
            Assert.Same(typeof(PreCompile), result3.CompilationResult.CompiledType);
        }

        private class TestView
        {
        }

        private class PreCompile
        {
        }

        public class DifferentView
        {
        }

        private CompilationResult ThrowsIfCalled(RelativeFileInfo file)
        {
            throw new Exception("Shouldn't be called");
        }
    }
}