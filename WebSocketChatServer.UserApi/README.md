# WebSocket Chat Server User API

ASP.NET Core를 기반으로 한 사용자 관리 및 인증 API 서버입니다. WebSocket 채팅 서버와 함께 사용되며, JWT 토큰 기반 인증과 MongoDB를 통한 사용자 데이터 관리를 제공합니다.

## 🚀 주요 기능

### 인증 & 보안
- **JWT 토큰 기반 인증**: 안전한 토큰 생성 및 검증
- **BCrypt 패스워드 해싱**: 강력한 비밀번호 보안
- **Bearer 토큰 인증**: Authorization 헤더를 통한 API 접근 제어

### 사용자 관리
- **회원가입**: 이메일/사용자명 중복 확인 포함
- **로그인**: 사용자명과 비밀번호를 통한 인증
- **프로필 관리**: 표시명, 아바타, 바이오 등 개인정보 수정
- **계정 설정**: 테마, 알림, 언어 설정 관리
- **계정 비활성화**: 안전한 계정 삭제 (논리적 삭제)

### MongoDB 연동
- **완전한 MongoDB 통합**: 사용자 데이터 영구 저장
- **인덱스 최적화**: 사용자명과 이메일에 대한 유니크 인덱스
- **유연한 스키마**: MongoDB의 문서 기반 데이터 구조 활용

### API 문서화
- **Swagger UI**: 자동 생성된 API 문서 (`/swagger`)
- **OpenAPI 3.0**: 표준화된 API 스펙
- **JWT 인증 테스트**: Swagger에서 직접 토큰 테스트 가능

## 📚 API 엔드포인트

### 🔐 인증 (Authentication)
| 메서드 | 엔드포인트 | 설명 | 인증 필요 |
|--------|-----------|------|-----------|
| `POST` | `/api/auth/login` | 사용자 로그인 | ❌ |
| `POST` | `/api/auth/register` | 회원가입 | ❌ |
| `GET` | `/api/auth/me` | 현재 사용자 정보 조회 | ✅ |
| `GET` | `/api/auth/check-username/{username}` | 사용자명 중복 확인 | ❌ |
| `GET` | `/api/auth/check-email/{email}` | 이메일 중복 확인 | ❌ |

### 👥 사용자 관리 (Users)
| 메서드 | 엔드포인트 | 설명 | 인증 필요 |
|--------|-----------|------|-----------|
| `GET` | `/api/users` | 사용자 목록 조회 (페이징) | ✅ |
| `GET` | `/api/users/{id}` | 특정 사용자 조회 | ✅ |
| `GET` | `/api/users/by-username/{username}` | 사용자명으로 사용자 조회 | ✅ |
| `PUT` | `/api/users/{id}` | 사용자 정보 수정 (본인만) | ✅ |
| `DELETE` | `/api/users/{id}` | 계정 비활성화 (본인만) | ✅ |

## 🔧 기술 스택

- **.NET 9.0**: 최신 .NET 프레임워크
- **ASP.NET Core**: 고성능 웹 API 프레임워크
- **MongoDB**: NoSQL 데이터베이스
- **JWT (Json Web Tokens)**: 토큰 기반 인증
- **BCrypt**: 패스워드 해싱
- **Swagger/OpenAPI**: API 문서화
- **Microsoft Aspire**: 클라우드 네이티브 오케스트레이션

## 📦 의존성 패키지

```xml
<PackageReference Include="Aspire.MongoDB.Driver" Version="9.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
<PackageReference Include="MongoDB.Driver" Version="2.30.0" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.1.2" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-beta.2" />
```

## ⚙️ 환경 설정

### appsettings.json
```json
{
  "Jwt": {
    "SecretKey": "Your-Super-Secret-Key-Here",
    "Issuer": "WebSocketChatServer",
    "Audience": "WebSocketChatServer",
    "ExpirationMinutes": "60"
  },
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "websocket_chat_db"
  }
}
```

### MongoDB 컬렉션 구조
```javascript
// users 컬렉션
{
  "_id": ObjectId("..."),
  "username": "user123",
  "email": "user@example.com",
  "passwordHash": "$2a$11$...",
  "isActive": true,
  "createdAt": ISODate("2024-01-01T00:00:00Z"),
  "lastLoginAt": ISODate("2024-01-01T12:00:00Z"),
  "profile": {
    "displayName": "사용자",
    "avatar": "https://example.com/avatar.jpg",
    "bio": "안녕하세요!",
    "settings": {
      "theme": "light",
      "notifications": true,
      "language": "ko"
    }
  }
}
```

## 🚀 실행 방법

### 1. Aspire를 통한 실행 (권장)
```bash
cd WebSocketChatServer.AppHost.AppHost
dotnet run
```

### 2. 직접 실행
```bash
cd WebSocketChatServer.UserApi
dotnet run
```

## 📊 모니터링

- **OpenTelemetry**: 분산 추적 및 메트릭
- **Prometheus**: 메트릭 수집 (`/metrics` 엔드포인트)
- **Grafana**: 대시보드 시각화
- **Health Checks**: 헬스 체크 엔드포인트 (`/health`)

## 🔒 보안 고려사항

1. **JWT Secret Key**: 프로덕션에서는 강력한 시크릿 키 사용
2. **HTTPS**: 프로덕션에서 반드시 HTTPS 사용
3. **CORS**: 필요한 도메인만 허용하도록 설정
4. **Rate Limiting**: API 호출 빈도 제한 구현 권장
5. **Input Validation**: 모든 입력 데이터 검증
6. **Password Policy**: 강력한 비밀번호 정책 구현

## 🔗 관련 프로젝트

- **WebSocketChatServer1**: 메인 WebSocket 채팅 서버
- **WebSocketChatServer.AppHost**: Aspire 오케스트레이션 호스트
- **WebSocketChatServer.ServiceDefaults**: 공통 서비스 설정

## 📝 사용 예시

### 1. 회원가입
```bash
curl -X POST "https://localhost:7001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "SecurePassword123!",
    "displayName": "테스트 사용자"
  }'
```

### 2. 로그인
```bash
curl -X POST "https://localhost:7001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "password": "SecurePassword123!"
  }'
```

### 3. 인증된 API 호출
```bash
curl -X GET "https://localhost:7001/api/auth/me" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## 🎯 향후 개발 계획

- [ ] OAuth 2.0 / OpenID Connect 지원
- [ ] 이메일 인증 기능
- [ ] 비밀번호 재설정 기능
- [ ] 2FA (이중 인증) 지원
- [ ] Rate Limiting 구현
- [ ] Redis를 통한 토큰 블랙리스트
- [ ] 사용자 역할 및 권한 관리
- [ ] 소셜 로그인 (Google, GitHub 등)

---

💡 **팁**: Swagger UI(`/swagger`)를 통해 API를 직접 테스트해볼 수 있습니다!
