# MongoDB 설정 가이드

## 연결 문제 해결

### 일반적인 연결 오류
1. **"No connection could be made because the target machine actively refused it"**
   - MongoDB 서버가 실행되지 않음
   - 방화벽이 포트를 차단함
   - 잘못된 호스트/포트 설정

2. **"Command insert failed: command insert requires authentication"**
   - MongoDB 인증이 필요하지만 자격 증명이 없음
   - 잘못된 사용자명/비밀번호

3. **연결 타임아웃**
   - 네트워크 지연 또는 서버 응답 없음

### MongoDB 서버 시작 방법

#### Windows에서 MongoDB 시작
```bash
# MongoDB 서비스 시작
net start MongoDB

# 또는 직접 실행
mongod --dbpath "C:\data\db"
```

#### Linux/macOS에서 MongoDB 시작
```bash
# systemd 사용
sudo systemctl start mongod

# 또는 직접 실행
mongod --dbpath /data/db
```

#### Docker로 MongoDB 실행
```bash
# 기본 MongoDB 컨테이너 실행
docker run -d -p 27017:27017 --name mongodb mongo

# 인증이 있는 MongoDB 실행
docker run -d -p 27017:27017 --name mongodb \
  -e MONGO_INITDB_ROOT_USERNAME=admin \
  -e MONGO_INITDB_ROOT_PASSWORD=password \
  mongo
```

## 애플리케이션 동작

### Fallback 메커니즘
현재 시스템은 MongoDB 연결 실패 시 자동으로 다음과 같이 동작합니다:

1. **MongoDB 서버가 실행되지 않는 경우**:
   - 5초 타임아웃으로 연결 시도
   - 실패 시 `NullCommandLogger` 사용
   - 애플리케이션은 정상 동작 (로깅만 콘솔에 출력)

2. **인증 실패 시**:
   - MongoDB 인증 오류 감지
   - 자동으로 `NullCommandLogger`로 전환
   - 경고 로그 출력 후 계속 동작

3. **네트워크 문제 시**:
   - 3초 타임아웃으로 데이터베이스 작업 시도
   - 타임아웃 시 `NullCommandLogger` 사용

### 시스템 상태 확인
```csharp
// 로그에서 다음 메시지들을 확인:
// "MongoDB connection successful" - 정상 연결
// "MongoDB client is not available" - 서버 미실행
// "MongoDB authentication failed" - 인증 오류
// "MongoDB connection timeout" - 네트워크 문제
```

### 1. 인증 없는 로컬 MongoDB
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  }
}
```

### 2. 인증이 필요한 MongoDB
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://username:password@localhost:27017/authDatabase"
  }
}
```

### 3. MongoDB Atlas (클라우드)
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb+srv://username:password@cluster.mongodb.net/database"
  }
}
```

### 4. 복제본 세트 연결
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://username:password@host1:27017,host2:27017,host3:27017/database?replicaSet=myReplicaSet"
  }
}
```

## MongoDB 인증 오류 해결

현재 시스템은 MongoDB 인증이 실패할 경우 자동으로 NullCommandLogger로 폴백됩니다.

### 일반적인 인증 오류
- `Command insert failed: command insert requires authentication`
- `MongoAuthenticationException`

### 해결 방법
1. **인증 정보 확인**: MongoDB에 사용자 계정이 올바르게 설정되어 있는지 확인
2. **연결 문자열 확인**: 사용자명, 비밀번호, 인증 데이터베이스가 올바른지 확인
3. **권한 확인**: 사용자가 해당 데이터베이스에 대한 읽기/쓰기 권한이 있는지 확인

### 시스템 동작
- MongoDB 연결이 실패하면 자동으로 NullCommandLogger 사용
- 애플리케이션은 계속 정상 동작하며, 기본 로깅만 수행
- MongoDB 연결이 복구되면 다시 정상적인 데이터베이스 로깅 사용

## 환경별 설정

### 개발 환경 (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDB": {
    "DatabaseName": "WebSocketChatServer_Dev"
  }
}
```

### 프로덕션 환경 (appsettings.Production.json)
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://prod_user:prod_password@prod-server:27017/authDatabase"
  },
  "MongoDB": {
    "DatabaseName": "WebSocketChatServer_Prod"
  }
}
```
