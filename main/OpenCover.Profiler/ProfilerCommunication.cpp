//
// OpenCover - S Wilde
//
// This source code is released under the MIT License; see the accompanying license file.
//
#include "StdAfx.h"
#include "ProfilerCommunication.h"
#include "ReleaseTrace.h"

#include <concrt.h>

#include <TlHelp32.h>

#include <sstream>

#define ONERROR_GOEXIT(hr) if (FAILED(hr)) goto Exit
#define MAX_MSG_SIZE 65536
#define COMM_WAIT_SHORT 10000
#define COMM_WAIT_LONG 60000

ProfilerCommunication::ProfilerCommunication() 
{
}

ProfilerCommunication::~ProfilerCommunication()
{
}

bool ProfilerCommunication::Initialise(TCHAR *key, TCHAR *ns)
{
	m_key = key;

	std::wstring sharedKey = key;
	sharedKey.append(_T("-1"));

    m_namespace = ns;

    m_mutexCommunication.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_Mutex_") + m_key).c_str());
    if (!m_mutexCommunication.IsValid()) return false;
    
	RELTRACE(_T("Initialised mutexes"));

    m_eventProfilerRequestsInformation.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_SendData_Event_") + sharedKey).c_str());
    if (!m_eventProfilerRequestsInformation.IsValid()) return false;

    m_eventInformationReadByProfiler.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_ChunkData_Event_") + sharedKey).c_str());
    if (!m_eventInformationReadByProfiler.IsValid()) return false;

    m_eventInformationReadyForProfiler.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_ReceiveData_Event_") + sharedKey).c_str());
    if (!m_eventInformationReadyForProfiler.IsValid()) return false;

    m_memoryCommunication.OpenFileMapping((m_namespace + _T("\\OpenCover_Profiler_Communication_MemoryMapFile_") + sharedKey).c_str());
    if (!m_memoryCommunication.IsValid()) return false;

    RELTRACE(_T("Initialised communication interface"));

    hostCommunicationActive = true;

    m_pMSG = (MSG_Union*)m_memoryCommunication.MapViewOfFile(0, 0, MAX_MSG_SIZE);

    ULONG bufferId =  0;
    if (AllocateBuffer(MAX_MSG_SIZE, bufferId))
    {
        std::wstring memoryKey;
        std::wstringstream stream ;
        stream << bufferId;
        stream >> memoryKey;

        memoryKey = m_key + memoryKey;

		m_eventProfilerRequestsInformation.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_SendData_Event_") + memoryKey).c_str());
		if (!m_eventProfilerRequestsInformation.IsValid()) return false;

		m_eventInformationReadByProfiler.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_ChunkData_Event_") + memoryKey).c_str());
		if (!m_eventInformationReadByProfiler.IsValid()) return false;

		m_eventInformationReadyForProfiler.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_ReceiveData_Event_") + memoryKey).c_str());
		if (!m_eventInformationReadyForProfiler.IsValid()) return false;

		m_memoryCommunication.OpenFileMapping((m_namespace + _T("\\OpenCover_Profiler_Communication_MemoryMapFile_") + memoryKey).c_str());
		if (!m_memoryCommunication.IsValid()) return false;

        m_pMSG = (MSG_Union*)m_memoryCommunication.MapViewOfFile(0, 0, MAX_MSG_SIZE);

		RELTRACE(_T("Re-initialised communication interface"));
        
        m_eventProfilerHasResults.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_SendResults_Event_") + memoryKey).c_str());
        if (!m_eventProfilerHasResults.IsValid()) return false;

        m_eventResultsHaveBeenReceived.Initialise((m_namespace + _T("\\OpenCover_Profiler_Communication_ReceiveResults_Event_") + memoryKey).c_str());
        if (!m_eventResultsHaveBeenReceived.IsValid()) return false;

        m_memoryResults.OpenFileMapping((m_namespace + _T("\\OpenCover_Profiler_Results_MemoryMapFile_") + memoryKey).c_str());
        if (!m_memoryResults.IsValid()) return false;

        m_pVisitPoints = (MSG_SendVisitPoints_Request*)m_memoryResults.MapViewOfFile(0, 0, MAX_MSG_SIZE);

        m_pVisitPoints->count = 0;

        RELTRACE(_T("Initialised results interface"));
    }

    return hostCommunicationActive;
}

void ProfilerCommunication::AddVisitPointToBuffer(ULONG uniqueId, ULONG msgType, ULONG threshold)
{
    if (uniqueId == 0) return;
	if (threshold != 0) 
	{
		if (m_thresholds[uniqueId] >= threshold)
			return;
		m_thresholds[uniqueId]++;
	}

	ATL::CComCritSecLock<ATL::CComAutoCriticalSection> lock(m_critResults);
    if (!hostCommunicationActive) return;
    m_pVisitPoints->points[m_pVisitPoints->count].UniqueId = (uniqueId | msgType);
    if (++m_pVisitPoints->count == VP_BUFFER_SIZE)
    {
        SendVisitPoints();
		::ZeroMemory(m_pVisitPoints, MAX_MSG_SIZE);
        m_pVisitPoints->count = 0;
    }
}

void ProfilerCommunication::SendVisitPoints()
{
    if (!hostCommunicationActive) return;
    try {
        DWORD dwSignal = m_eventProfilerHasResults.SignalAndWait(m_eventResultsHaveBeenReceived, COMM_WAIT_SHORT);
        if (WAIT_OBJECT_0 != dwSignal) throw CommunicationException(dwSignal, COMM_WAIT_SHORT);
        m_eventResultsHaveBeenReceived.Reset();
    } catch (CommunicationException ex) {
        RELTRACE(_T("ProfilerCommunication::SendVisitPoints() => Communication (Results channel) with host has failed (0x%x, %d)"), 
			ex.getReason(), ex.getTimeout());
        hostCommunicationActive = false;
    }
    return;
}

bool ProfilerCommunication::GetPoints(mdToken functionToken, WCHAR* pModulePath, 
    WCHAR* pAssemblyName, std::vector<SequencePoint> &seqPoints, std::vector<BranchPoint> &brPoints)
{
    if (!hostCommunicationActive) return false;

    bool ret = GetSequencePoints(functionToken, pModulePath, pAssemblyName, seqPoints);
     
    GetBranchPoints(functionToken, pModulePath, pAssemblyName, brPoints);

    return ret;
}

bool ProfilerCommunication::GetSequencePoints(mdToken functionToken, WCHAR* pModulePath,  
    WCHAR* pAssemblyName, std::vector<SequencePoint> &points)
{
    if (!hostCommunicationActive) return false;

    points.clear();

    RequestInformation(
        [=]
        {
            m_pMSG->getSequencePointsRequest.type = MSG_GetSequencePoints;
            m_pMSG->getSequencePointsRequest.functionToken = functionToken;
            wcscpy_s(m_pMSG->getSequencePointsRequest.szModulePath, pModulePath);
            wcscpy_s(m_pMSG->getSequencePointsRequest.szAssemblyName, pAssemblyName);
        }, 
        [=, &points]()->BOOL
        {
            for (int i=0; i < m_pMSG->getSequencePointsResponse.count;i++)
                points.push_back(m_pMSG->getSequencePointsResponse.points[i]); 
            BOOL hasMore = m_pMSG->getSequencePointsResponse.hasMore;
			::ZeroMemory(m_pMSG, MAX_MSG_SIZE);
			return hasMore;
        }
        , COMM_WAIT_SHORT
        , _T("GetSequencePoints"));

    return (points.size() != 0);
}

bool ProfilerCommunication::GetBranchPoints(mdToken functionToken, WCHAR* pModulePath, 
    WCHAR* pAssemblyName, std::vector<BranchPoint> &points)
{
    if (!hostCommunicationActive) return false;
    
    points.clear();

    RequestInformation(
        [=]
        {
            m_pMSG->getBranchPointsRequest.type = MSG_GetBranchPoints;
            m_pMSG->getBranchPointsRequest.functionToken = functionToken;
            wcscpy_s(m_pMSG->getBranchPointsRequest.szModulePath, pModulePath);
            wcscpy_s(m_pMSG->getBranchPointsRequest.szAssemblyName, pAssemblyName);
        }, 
        [=, &points]()->BOOL
        {
            for (int i=0; i < m_pMSG->getBranchPointsResponse.count;i++)
                points.push_back(m_pMSG->getBranchPointsResponse.points[i]); 
            BOOL hasMore = m_pMSG->getBranchPointsResponse.hasMore;
 		    ::ZeroMemory(m_pMSG, MAX_MSG_SIZE);
			return hasMore;
        }
        , COMM_WAIT_SHORT
        , _T("GetBranchPoints"));

    return (points.size() != 0);
}

bool ProfilerCommunication::TrackAssembly(WCHAR* pModulePath, WCHAR* pAssemblyName)
{
    if (!hostCommunicationActive) return false;

    bool response = false;
    RequestInformation(
        [=]()
        {
            m_pMSG->trackAssemblyRequest.type = MSG_TrackAssembly; 
            wcscpy_s(m_pMSG->trackAssemblyRequest.szModulePath, pModulePath);
            wcscpy_s(m_pMSG->trackAssemblyRequest.szAssemblyName, pAssemblyName);
        }, 
        [=, &response]()->BOOL
        {
            response =  m_pMSG->trackAssemblyResponse.bResponse == TRUE;
			::ZeroMemory(m_pMSG, MAX_MSG_SIZE);
            return FALSE;
        }
        , COMM_WAIT_LONG
        , _T("TrackAssembly"));

    return response;
}

bool ProfilerCommunication::TrackMethod(mdToken functionToken, WCHAR* pModulePath, WCHAR* pAssemblyName, ULONG &uniqueId)
{
    if (!hostCommunicationActive) return false;

    bool response = false;
    RequestInformation(
        [=]()
        {
            m_pMSG->trackMethodRequest.type = MSG_TrackMethod; 
            m_pMSG->trackMethodRequest.functionToken = functionToken;
            wcscpy_s(m_pMSG->trackMethodRequest.szModulePath, pModulePath);
            wcscpy_s(m_pMSG->trackMethodRequest.szAssemblyName, pAssemblyName);
        }, 
        [=, &response, &uniqueId]()->BOOL
        {
            response =  m_pMSG->trackMethodResponse.bResponse == TRUE;
            uniqueId = m_pMSG->trackMethodResponse.ulUniqueId;
			::ZeroMemory(m_pMSG, MAX_MSG_SIZE);
            return FALSE;
        }
        , COMM_WAIT_SHORT
        , _T("TrackMethod"));

    return response;
}

bool ProfilerCommunication::AllocateBuffer(LONG bufferSize, ULONG &bufferId)
{
    CScopedLock<CMutex> lock(m_mutexCommunication);
    if (!hostCommunicationActive) return false;

    bool response = false;

    RequestInformation(
        [=]()
        {
            m_pMSG->allocateBufferRequest.type = MSG_AllocateMemoryBuffer; 
            m_pMSG->allocateBufferRequest.lBufferSize = bufferSize;
        }, 
        [=, &response, &bufferId]()->BOOL
        {
            response =  m_pMSG->allocateBufferResponse.bResponse == TRUE;
            bufferId = m_pMSG->allocateBufferResponse.ulBufferId;
			::ZeroMemory(m_pMSG, MAX_MSG_SIZE);
            return FALSE;
        }
        , COMM_WAIT_SHORT
        , _T("AllocateBuffer"));

    return response;
}

template<class BR, class PR>
void ProfilerCommunication::RequestInformation(BR buildRequest, PR processResults, DWORD dwTimeout, tstring message)
{
	ATL::CComCritSecLock<ATL::CComAutoCriticalSection> lock(m_critComms);
    if (!hostCommunicationActive) return;

	try {
        buildRequest();
    
        DWORD dwSignal = m_eventProfilerRequestsInformation.SignalAndWait(m_eventInformationReadyForProfiler, dwTimeout);
		if (WAIT_OBJECT_0 != dwSignal) throw CommunicationException(dwSignal, dwTimeout);
    
        m_eventInformationReadyForProfiler.Reset();

        BOOL hasMore = FALSE;
        do
        {
            hasMore = processResults();

            if (hasMore)
            {
                dwSignal = m_eventInformationReadByProfiler.SignalAndWait(m_eventInformationReadyForProfiler, COMM_WAIT_SHORT);
                if (WAIT_OBJECT_0 != dwSignal) throw CommunicationException(dwSignal, COMM_WAIT_SHORT);
            
                m_eventInformationReadyForProfiler.Reset();
            }
        }while (hasMore);

        m_eventInformationReadByProfiler.Set();
    } catch (CommunicationException ex) {
        RELTRACE(_T("ProfilerCommunication::RequestInformation(...) => Communication (Chat channel - %s) with host has failed (0x%x, %d)"),  
			message.c_str(), ex.getReason(), ex.getTimeout());
        hostCommunicationActive = false;
    }
}
