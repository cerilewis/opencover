﻿using System;
using System.Linq;
using Moq;
using NUnit.Framework;
using OpenCover.Framework.Communication;
using OpenCover.Framework.Model;
using OpenCover.Framework.Service;
using OpenCover.Test.MoqFramework;

namespace OpenCover.Test.Framework.Communication
{
    using OpenCover.Framework.Symbols;

    [TestFixture]
    public class MessageHandlerTests :
        UnityAutoMockContainerBase<IMessageHandler, MessageHandler>
    {
        [Test]
        public void Handles_MSG_TrackAssembly()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_TrackAssembly_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_TrackAssembly_Request());

            // act
            Instance.StandardMessage(MSG_Type.MSG_TrackAssembly, IntPtr.Zero, (x) => { });

            // assert
            Container.GetMock<IProfilerCommunication>()
                .Verify(x=>x.TrackAssembly(It.IsAny<string>(), It.IsAny<string>()), Times.Once());

        }

        [Test]
        public void TackAssembly_Uses_IModuleLocator_If_ModulePath_StartsWith_AtSymbol()
        {
            // Arrange
            Container.GetMock<IMarshalWrapper>()
                     .Setup(x => x.PtrToStructure<MSG_TrackAssembly_Request>(It.IsAny<IntPtr>()))
                     .Returns(new MSG_TrackAssembly_Request { modulePath = "@123456", assemblyName = "AssemblyName" });
            Container.GetMock<IModuleLocator>()
                     .Setup(x => x.LocateForAssembly(It.IsAny<string>()))
                     .Returns("ModulePath");
            
            // Act
            Instance.StandardMessage(MSG_Type.MSG_TrackAssembly, IntPtr.Zero, (x) => { });

            // Assert
            Container.GetMock<IModuleLocator>()
                .Verify(x => x.LocateForAssembly(It.IsAny<string>()), Times.Once());
            Container.GetMock<IProfilerCommunication>()
                .Verify(x => x.TrackAssembly("ModulePath", It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void Handles_MSG_TrackMethod()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_TrackMethod_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_TrackMethod_Request());

            // act
            Instance.StandardMessage(MSG_Type.MSG_TrackMethod, IntPtr.Zero, (x) => { });

            // assert
            uint uniqueId;
            Container.GetMock<IProfilerCommunication>()
                .Verify(x => x.TrackMethod(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out uniqueId), Times.Once());

        }

        [Test]
        public void GetSequencePoints_Uses_IModuleLocator_If_ModulePath_StartsWith_AtSymbol()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetSequencePoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetSequencePoints_Request { modulePath = "@123456", assemblyName = "AssemblyName" });
            Container.GetMock<IModuleLocator>()
                .Setup(x => x.LocateForAssembly(It.IsAny<string>()))
                .Returns("ModulePath");


            // act
            Instance.StandardMessage(MSG_Type.MSG_GetSequencePoints, IntPtr.Zero, (x) => { });

            // assert
            InstrumentationPoint[] points;
            Container.GetMock<IModuleLocator>()
                .Verify(x => x.LocateForAssembly(It.IsAny<string>()), Times.Once());
            Container.GetMock<IProfilerCommunication>()
                .Verify(x => x.GetSequencePoints("ModulePath", It.IsAny<string>(), It.IsAny<int>(), out points), Times.Once());

        }

        [Test]
        public void Handles_MSG_GetSequencePoints()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetSequencePoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetSequencePoints_Request());

            // act
            Instance.StandardMessage(MSG_Type.MSG_GetSequencePoints, IntPtr.Zero, (x) => { });

            // assert
            InstrumentationPoint[] points;
            Container.GetMock<IProfilerCommunication>()
                .Verify(x => x.GetSequencePoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points), Times.Once());

        }
        
        [Test]
        public void Handles_MSG_GetSequencePoints_Small()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetSequencePoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetSequencePoints_Request());

            var points = Enumerable.Repeat(new InstrumentationPoint(), 2).ToArray();
            Container.GetMock<IProfilerCommunication>()
                .Setup(x => x.GetSequencePoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points));

            var chunked = false;
            // act
            Instance.StandardMessage(MSG_Type.MSG_GetSequencePoints, IntPtr.Zero, (x) => { chunked = true; });
            
            // assert
            Container.GetMock<IMarshalWrapper>()
                .Verify(x=>x.StructureToPtr(It.IsAny<MSG_SequencePoint>(), It.IsAny<IntPtr>(), It.IsAny<bool>()), Times.Exactly(2));

            Assert.False(chunked);
        }

        [Test]
        public void Handles_MSG_GetSequencePoints_Large_StartsToChunk()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetSequencePoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetSequencePoints_Request());

            var points = Enumerable.Repeat(new InstrumentationPoint(), 100).ToArray();

            //var points = new[] { new SequencePoint(), new SequencePoint(), new SequencePoint(), new SequencePoint(), new SequencePoint(), new SequencePoint() };
            Container.GetMock<IProfilerCommunication>()
                .Setup(x => x.GetSequencePoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points));
            
            var chunked = false;
            // act
            Instance.StandardMessage(MSG_Type.MSG_GetSequencePoints, IntPtr.Zero, (x) => { chunked = true; });

            // assert
            Container.GetMock<IMarshalWrapper>()
                .Verify(x => x.StructureToPtr(It.IsAny<MSG_SequencePoint>(), It.IsAny<IntPtr>(), It.IsAny<bool>()), Times.Exactly(100));

            Assert.True(chunked);

        }

        [Test]
        public void Handles_MSG_GetBranchPoints()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetBranchPoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetBranchPoints_Request());

            // act
            Instance.StandardMessage(MSG_Type.MSG_GetBranchPoints, IntPtr.Zero, (x) => { });

            // assert
            BranchPoint[] points;
            Container.GetMock<IProfilerCommunication>()
                .Verify(x => x.GetBranchPoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points), Times.Once());

        }

        [Test]
        public void Handles_MSG_GetBranchPoints_Small()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetBranchPoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetBranchPoints_Request());

            var points = Enumerable.Repeat(new BranchPoint(), 2).ToArray();
            Container.GetMock<IProfilerCommunication>()
                .Setup(x => x.GetBranchPoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points));

            var chunked = false;
            // act
            Instance.StandardMessage(MSG_Type.MSG_GetBranchPoints, IntPtr.Zero, (x) => { chunked = true; });

            // assert
            Container.GetMock<IMarshalWrapper>()
                .Verify(x => x.StructureToPtr(It.IsAny<MSG_BranchPoint>(), It.IsAny<IntPtr>(), It.IsAny<bool>()), Times.Exactly(2));

            Assert.False(chunked);
        }

        [Test]
        public void Handles_MSG_GetBranchPoints_Large_StartsToChunk()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetBranchPoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetBranchPoints_Request());

            var points = Enumerable.Repeat(new BranchPoint(), 100).ToArray();

            Container.GetMock<IProfilerCommunication>()
                .Setup(x => x.GetBranchPoints(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), out points));

            var chunked = false;
            // act
            Instance.StandardMessage(MSG_Type.MSG_GetBranchPoints, IntPtr.Zero, (x) => { chunked = true; });

            // assert
            Container.GetMock<IMarshalWrapper>()
                .Verify(x => x.StructureToPtr(It.IsAny<MSG_BranchPoint>(), It.IsAny<IntPtr>(), It.IsAny<bool>()), Times.Exactly(100));

            Assert.True(chunked);
        }

        [Test]
        public void GetBranchPoints_Uses_IModuleLocator_If_ModulePath_StartsWith_AtSymbol()
        {
            // arrange 
            Container.GetMock<IMarshalWrapper>()
                .Setup(x => x.PtrToStructure<MSG_GetBranchPoints_Request>(It.IsAny<IntPtr>()))
                .Returns(new MSG_GetBranchPoints_Request { modulePath = "@123456", assemblyName = "AssemblyName" });

            var chunked = false;

            // act
            Instance.StandardMessage(MSG_Type.MSG_GetBranchPoints, IntPtr.Zero, (x) => { chunked = true; });

            // assert
            Container.GetMock<IModuleLocator>()
                .Verify(x => x.LocateForAssembly(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void ReadSize_Returns()
        {
            var size = Instance.ReadSize;
            Assert.AreNotEqual(0, size);
        }
    }
}
