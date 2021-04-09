using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Piglet
{
    /// <summary>
    /// These tests are used to confirm that the various
    /// relection-based helper methods used by ProjectBrowserDragAndDrop
    /// work correctly across Unity versions.
    /// </summary>
    public class ProjectBrowserReflectionTest
    {
        [Test]
        public void ProjectBrowserTypeTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .ProjectBrowserType);
        }

        [Test]
        public void LastInteractedProjectBrowserTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .LastInteractedProjectBrowser);
        }

        [Test]
        public void GetActiveFolderTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .GetActiveFolderPath);
        }

        [Test]
        public void ListAreaRectTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .ListAreaRect);
        }

        [Test]
        public void ViewModeTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .ViewMode);
        }

        [Test]
        public void SearchFilterTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .SearchFilter);
        }

        [Test]
        public void IsSearchingTest()
        {
            Assert.IsNotNull(ProjectBrowserReflection
                .IsSearching);
        }
    }
}