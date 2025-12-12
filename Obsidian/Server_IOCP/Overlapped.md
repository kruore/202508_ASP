
---

`OVERLAPPED` 구조체


### . 핵심 준비물: `OVERLAPPED` 구조체란?

모든 비동기 I/O 모델의 공통점은 이 구조체를 사용한다는 것입니다. 이것은 **"비동기 작업 주문서(영수증)"**와 같습니다.

C

```
typedef struct _OVERLAPPED {
  ULONG_PTR Internal;     // OS가 사용 (에러 코드 등 상태 저장)
  ULONG_PTR InternalHigh; // OS가 사용 (전송된 바이트 수 저장)
  union {
    struct {
      DWORD Offset;       // 파일 I/O 시 읽을 위치 (하위 32비트)
      DWORD OffsetHigh;   // 파일 I/O 시 읽을 위치 (상위 32비트)
    } DUMMYSTRUCTNAME;
    PVOID Pointer;
  } DUMMYUNIONNAME;
  HANDLE  hEvent;         // **핵심: 완료 알림을 받을 이벤트 핸들**
} OVERLAPPED, *LPOVERLAPPED;
```

- 함수 호출(예: `WSARecv`) 시 이 구조체의 주소를 넘기면, I/O가 끝난 뒤 OS가 이 구조체에 **결과(성공 여부, 바이트 수)**를 채워줍니다.
    

---

### 1. 방식 1: 디바이스 커널 객체 시그널링 (Device Kernel Object Signaling)

가장 원시적인 형태의 Overlapped I/O입니다.

- **동작 원리:**
    
    1. `ReadFile`이나 `WSARecv` 호출 시 `OVERLAPPED` 구조체를 넘깁니다.
        
    2. 함수는 즉시 리턴됩니다.
        
    3. 작업이 완료되면, **파일 핸들(소켓 핸들) 자체**가 `Signaled` 상태(신호 받은 상태)로 변합니다.
        
    4. `WaitForSingleObject(hSocket, ...)`로 대기하다가 신호를 받습니다.
        
- **치명적 단점:**
    
    - **구분 불가:** 하나의 소켓으로 여러 `WSARecv`를 요청했다면, 소켓 핸들이 신호를 받았을 때 **어떤 요청이 끝난 건지 알 방법이 없습니다.**
        
    - 따라서 실무에서는 거의 사용되지 않습니다.
        

---

### 2. 방식 2: 이벤트 커널 객체 시그널링 (Event Kernel Object Signaling)

방식 1의 단점을 보완하기 위해, `OVERLAPPED` 구조체 안에 있는 `hEvent` 필드를 활용하는 방식입니다. **`WSAWaitForMultipleEvents`** 모델이라고도 합니다.

- **동작 원리:**
    
    1. 개발자가 `CreateEvent`로 이벤트 객체를 생성하여 `OVERLAPPED.hEvent`에 넣어줍니다.
        
    2. I/O 요청(`WSARecv`)을 보냅니다.
        
    3. 작업이 완료되면, OS는 소켓이 아닌 **구조체 안의 `hEvent`를 Signaled 상태**로 만듭니다.
        
    4. `WSAWaitForMultipleEvents` 함수로 이벤트를 기다립니다.
        
- **장점:**
    
    - 각 요청(I/O)마다 서로 다른 이벤트 객체를 달아두면, 어떤 작업이 끝났는지 정확히 식별 가능합니다.
        
- **한계 (64개의 저주):**
    
    - `WSAWaitForMultipleEvents`가 한 번에 감시할 수 있는 이벤트의 개수는 최대 **64개**(`WSA_MAXIMUM_WAIT_EVENTS`)입니다.
        
    - 수천 개의 접속을 처리하려면 스레드 하나당 64개씩 관리하는 복잡한 스레드 풀링을 직접 구현해야 합니다. (확장성 부족)
        

---

### 3. 방식 3: 얼러터블 I/O (Alertable I/O)와 APC

이벤트 객체를 기다리는 것조차 낭비라고 생각하여 나온 방식입니다. **"완료 루틴(Completion Routine)"** 혹은 **콜백(Callback)** 방식입니다.

- **동작 원리 (APC: Asynchronous Procedure Call):**
    
    1. `WSARecv`나 `ReadFileEx`를 호출할 때, 마지막 인자로 **함수 포인터(Completion Routine)**를 넘깁니다.
        
    2. I/O가 완료되면, OS는 해당 결과를 들고 잠시 대기합니다.
        
    3. 해당 I/O를 요청했던 스레드가 **"경고 가능한 상태(Alertable State)"**가 되면, OS가 스레드의 흐름을 가로채서 콜백 함수를 강제로 실행시킵니다.
        
    4. _Alertable State란?_ 스레드가 `SleepEx`, `WaitForSingleObjectEx` 등의 함수를 호출하여 "나 할 일 없으니 콜백 있으면 실행해"라고 명시한 상태입니다.
        
- **장점:**
    
    - 이벤트 객체를 관리하거나 64개 제한에 걸릴 일이 없습니다.
        
- **치명적 단점 (스레드 종속성):**
    
    - **반드시 요청한 스레드가 처리를 해야 합니다.**
        
    - 만약 요청한 스레드가 복잡한 연산을 하느라 `Alertable State`로 진입하지 않으면, I/O가 완료되어도 콜백이 실행되지 않고 계속 밀립니다.
        
    - 부하 분산(Load Balancing)이 불가능합니다.