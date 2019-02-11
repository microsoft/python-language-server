// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using static Microsoft.Python.LanguageServer.Tests.FluentAssertions.DocstringAssertions;

namespace Microsoft.Python.LanguageServer.Tests {
    [TestClass]
    public class DocstringConverterTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize()
            => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [DataRow("A\nB", "A\nB")]
        [DataRow("A\n\nB", "A\n\nB")]
        [DataRow("A\n\nB", "A\n\nB")]
        [DataRow("A\n    B", "A\nB")]
        [DataRow("    A\n    B", "A\nB")]
        [DataRow("\nA\n    B", "A\n    B")]
        [DataRow("\n    A\n    B", "A\nB")]
        [DataRow("\nA\nB\n", "A\nB")]
        [DataRow("  \n\nA \n    \nB  \n    ", "A\n\nB")]
        [DataTestMethod, Priority(0)]
        public void PlaintextIndention(string docstring, string expected) {
            docstring.Should().ConvertToPlaintext(expected);
        }

        [TestMethod, Priority(0)]
        public void NormalText() {
            var docstring = @"This is just some normal text
that extends over multiple lines. This will appear
as-is without modification.
";

            var markdown = @"This is just some normal text
that extends over multiple lines. This will appear
as-is without modification.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void InlineLitereals() {
            var docstring = @"This paragraph talks about ``foo``
which is related to :something:`bar`, and probably `qux`:something_else:.
";

            var markdown = @"This paragraph talks about `foo`
which is related to `bar`, and probably `qux`.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void Headings() {
            var docstring = @"Heading 1
=========

Heading 2
---------

Heading 3
~~~~~~~~~
";

            var markdown = @"Heading 1
=========

Heading 2
---------

Heading 3
---------
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [DataRow(@"*foo*", @"\*foo\*")]
        [DataRow(@"``*foo*``", @"`*foo*`")]
        [DataRow(@"~foo~~bar~", @"\~foo\~\~bar\~")]
        [DataRow(@"``~foo~~bar~``", @"`~foo~~bar~`")]
        [DataRow(@"__init__", @"\_\_init\_\_")]
        [DataRow(@"``__init__``", @"`__init__`")]
        [DataTestMethod, Priority(0)]
        public void EscapedCharacters(string docstring, string markdown) {
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void CopyrightAndLicense() {
            var docstring = @"This is a test.

:copyright: Fake Name
:license: ABCv123
";

            var markdown = @"This is a test.

:copyright: Fake Name

:license: ABCv123
";

            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void CommonRestFieldLists() {
            var docstring = @"This function does something.

:param foo: This is a description of the foo parameter
    which does something interesting.
:type foo: Foo
:param bar: This is a description of bar.
:type bar: Bar
:return: Something else.
:rtype: Something
:raises ValueError: If something goes wrong.
";

            var markdown = @"This function does something.

:param foo: This is a description of the foo parameter
    which does something interesting.

:type foo: Foo

:param bar: This is a description of bar.

:type bar: Bar

:return: Something else.

:rtype: Something

:raises ValueError: If something goes wrong.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void Doctest() {
            var docstring = @"This is a doctest:

>>> print('foo')
foo
";

            var markdown = @"This is a doctest:

```
>>> print('foo')
foo
```
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void DoctestIndented() {
            var docstring = @"This is a doctest:

    >>> print('foo')
    foo
";

            var markdown = @"This is a doctest:

```
>>> print('foo')
foo
```
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void DoctestTextAfter() {
            var docstring = @"This is a doctest:

>>> print('foo')
foo

This text comes after.
";

            var markdown = @"This is a doctest:

```
>>> print('foo')
foo
```

This text comes after.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void DoctestIndentedTextAfter() {
            var docstring = @"This is a doctest:

    >>> print('foo')
    foo
  This line has a different indent.
";

            var markdown = @"This is a doctest:

```
>>> print('foo')
foo
```

This line has a different indent.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void MarkdownStyleBacktickBlock() {
            var docstring = @"Backtick block:

```
print(foo_bar)

if True:
    print(bar_foo)
```

And some text after.
";

            var markdown = @"Backtick block:

```
print(foo_bar)

if True:
    print(bar_foo)
```

And some text after.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void RestLiteralBlock() {
            var docstring = @"
Take a look at this code::

    if foo:
        print(foo)
    else:
        print('not foo!')

This text comes after.
";

            var markdown = @"Take a look at this code:

```
if foo:
    print(foo)
else:
    print('not foo!')
```

This text comes after.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void RestLiteralBlockExtraSpace() {
            var docstring = @"
Take a look at this code::




    if foo:
        print(foo)
    else:
        print('not foo!')

This text comes after.
";

            var markdown = @"Take a look at this code:

```
if foo:
    print(foo)
else:
    print('not foo!')
```

This text comes after.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void RestLiteralBlockEmptyDoubleColonLine() {
            var docstring = @"
::

    if foo:
        print(foo)
    else:
        print('not foo!')
";

            var markdown = @"```
if foo:
    print(foo)
else:
    print('not foo!')
```
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void RestLiteralBlockNoIndentOneLiner() {
            var docstring = @"
The next code is a one-liner::

print(a + foo + 123)

And now it's text.
";

            var markdown = @"The next code is a one-liner:

```
print(a + foo + 123)
```

And now it's text.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void DirectiveRemoval() {
            var docstring = @"This is a test.

.. ignoreme:: example

This text is in-between.

.. versionadded:: 1.0
    Foo was added to Bar.

.. admonition:: Note
    
    This paragraph appears inside the admonition
    and spans multiple lines.

This text comes after.
";

            var markdown = @"This is a test.

This text is in-between.

This text comes after.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void ClassDirective() {
            var docstring = @"
.. class:: FooBar()
    This is a description of ``FooBar``.

``FooBar`` is interesting.
";

            var markdown = @"```
FooBar()
```

This is a description of `FooBar`.

`FooBar` is interesting.
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void UnfinishedBacktickBlock() {
            var docstring = @"```
something
";

            var markdown = @"```
something
```
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void UnfinishedInlineLiteral() {
            var docstring = @"`oops
";

            var markdown = @"`oops`";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void DashList() {
            var docstring = @"
This is a list:
  - Item 1
  - Item 2
";

            var markdown = @"This is a list:
  - Item 1
  - Item 2
";
            docstring.Should().ConvertToMarkdown(markdown);
        }

        [TestMethod, Priority(0)]
        public void AsteriskList() {
            var docstring = @"
This is a list:
  * Item 1
  * Item 2
";

            var markdown = @"This is a list:
  * Item 1
  * Item 2
";
            docstring.Should().ConvertToMarkdown(markdown);
        }
    }
}
