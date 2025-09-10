# CupTower_Server
(클라이언트 프로젝트 코드는 [https://github.com/manmarru/CupTower](https://github.com/manmarru/CupTower) 입니다.)  

[시연 영상](https://youtu.be/LJoUJH3Uyo0)  
[프로젝트 개발 기록서](https://drive.google.com/file/d/1L8gsk_rO1SqeiQrtgzJS18Z3tA8TThwx/view?usp=drive_link)  
CupTower 프로젝트에서 만들어서 사용한 서버입니다.  
Server.cs에 정의된 MAXUSER 값을 조절해서 접속 인원을 조절할 수 있습니다. (1~3, 테스트용이 아니라면 3)  
메인 쓰레드는 종료 대기 상태로, 통신 쓰레드들의 종료를 기다리는 상태로 남아 있기 때문에 게임이 종료되고 유저들이 연결을 해제하면 프로그램이 자동으로 종료됩니다.  

# 기능
- 유저들의 유효한 행동 요청을 모든 유저에게 브로드캐스팅  
- 어느 유저의 차례가 됐는지 지시  
- 승자 판정 및 게임 진행 상태 재설정  
- 게임 종료 지시  

# 동기화
- 전송 도중 데이터가 오염되는 것을 방지하기 위해 송/수신 데이터는 각 쓰레드에서 개별적인 지역 메모리를 사용합니다.  
- 한 유저에게 동시에 데이터를 전송하면서 발생하는 데이터 혼선을 막기 위해 lock을 사용해서 같은 유저에 대해서 동시에 한 쓰레드만 데이터를 전송할 수 있게 했습니다.  

# 주요 코드
- [Server.cs](https://github.com/manmarru/CupTower_Server/blob/main/CupTower_Server/Server%3B.cs)
