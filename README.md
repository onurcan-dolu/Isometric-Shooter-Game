# Isometric Shooter

Unity ile Mirror networking ve Steam entegrasyonu kullanılarak geliştirilmiş bir **multiplayer isometric shooter**. Oyuncular, küçük bir arenada döndürülebilir kamerayla ateşli silahlar kullanarak AI düşmanlarıyla savaşır.

## Özellikler

- **Multiplayer** - Steam lobby tabanlı P2P networking (Mirror + FizzyFacepunch)
- **Server-Authoritative** - Tüm oyun mantığı (hasar, spawn, AI) sunucu tarafında çalışır
- **Position Sync** - Sahip (`[Command]`) hareketi gönderir, uzak istemciler `[SyncVar]` üzerinden yumuşatır
- **Döndürülebilir Kamera** - Q/E tuşları kamera yörüngesini oyuncu etrafında döndürür (Cinemachine)
- **AI Düşmanlar** - NavMesh pathfinding, FOV algılama ve line-of-sight kontrolü ile Patrol/Combat state machine
- **Hitbox Sistemi** - IDamageable arayüzüyle Kafa (2x), Gövde (1.5x), Uzuv (1x) hasar çarpanları
- **Ragdoll Fiziği** - Mermi çarpmasından doğan yönlü darbe kuvveti, animasyon ↔ ragdoll geçişleri
- **Mermi Dağılımı** - Sürekli ateşte artar, boştayken toparlanır
- **Malzemeye Göre Efektler** - Mermi çarpmasında Wood/Brick/Metal/Enemy parçacıkları
- **Taret Sistemi** - En yakın yaşayan oyuncuyu hedefleyen otomatik makineli tüfek
- **Oyuncu Respawn** - `ReplacePlayerForConnection` ile sunucu tarafından yönetilen respawn

## Mimari

```
Assets/Scripts/
├── Core/
│   ├── IDamageable.cs              # Hasar alabilen varlıklar için arayüz
│   ├── Health.cs                   # SyncVar'lı server-authoritative can sistemi
│   ├── DummyHealth.cs              # Otomatik respawn olan eğitim hedefi
│   ├── HitboxPart.cs               # Vücut parçası hasar dağıtımı + TargetRpc geri bildirimi
│   ├── Bullet.cs                   # Server-authoritative raycast çarpışmalı mermi
│   ├── RagdollController.cs        # Animasyon ↔ ragdoll durum geçişleri
│   ├── PlayerRespawnManager.cs     # Singleton respawn orkestratörü
│   └── DamagePopup.cs              # Yüzen hasar sayıları
├── Player/
│   ├── PlayerController.cs         # Oyuncu hareketi (WASD, çömelme, koşu, fare nişanı) + position sync
│   ├── CharacterAnimationController.cs # [SyncVar] ile ağ senkronlu animasyon
│   ├── ShootingController.cs       # Silah nişanı, mermi spawn'ı, namlu efektleri
│   ├── PlayerUIController.cs       # Sadece yerel HUD (can göstergesi)
│   ├── WorldCrosshair.cs           # Yüzeyler üzerine yansıtılan 3D nişangah
│   └── TopDownCinemachineController.cs # Q/E dönüşlü Cinemachine top-down kamera
├── AI/
│   ├── AICharacterController.cs    # AI hareketi (Rigidbody tabanlı)
│   ├── AICharacterAnimationController.cs # Server-authoritative AI animasyonu
│   ├── AIShootingController.cs     # AI silah nişanı + mermi spawn'ı
│   ├── AISimpleBehaviour.cs        # AI beyni: Patrol + Combat durumları
│   ├── AISpawner.cs                # Ağ senkronlu AI spawn ve respawn
│   └── MachineGunController.cs     # Otomatik taret hedefleme sistemi
└── Network/
    └── SteamNetworkManager.cs      # Steam lobby oluşturma/katılma
```

## Tasarım Desenleri

| Desen | Uygulama |
|---|---|
| **Interface Segregation** | `IDamageable` arayüzü hasar sistemini can uygulamalarından ayırır |
| **Component Pattern** | Her varlık odaklı MonoBehaviours'lardan oluşur (hareket, ateş, can, ragdoll) |
| **Singleton** | `PlayerRespawnManager.Instance`, `SteamNetworkManager.Instance` |
| **Observer/Event** | `Health.OnDeath`, `Health.OnHealthChanged` C# event'leri |
| **Server-Authoritative** | Tüm oyun durumu değişiklikleri sunucuda doğrulanır, istemciler yalnızca girdi gönderir |

## Teknoloji Yığını

| Teknoloji | Amaç |
|---|---|
| Unity 2022 (URP 14) | Görselleştirme pipeline'ı |
| Mirror | Networking framework |
| Steamworks / Facepunch | Steam P2P networking |
| Cinemachine | Kamera sistemi |
| Unity AI Navigation | NavMesh pathfinding |
| Animation Rigging | IK ve avatar kısıtlamaları |

## Kurulum

1. Depoyu klonlayın
2. Projeyi **Unity 2022.3+** ile açın
3. `Assets/Scenes/Menu.unity` veya `Assets/Scenes/Lobby.unity` sahnesini açın
4. Multiplayer testi için iki ayrı editor instance çalıştırın (host + client) — Steam lobileri LAN üzerinden çalışır
5. Host bir Steam lobby oluşturur (ID otomatik olarak panoya kopyalanır), diğer oyuncular ID ile katılır

## Kontroller

| Tuş | Eylem |
|---|---|
| WASD | Hareket |
| Fare | Nişan alma |
| Sol Tık | Ateş etme |
| Sağ Tık | Nişangahı kaldırma |
| Q / E | Kamera döndürme |
| Sol Shift | Koşma |
| Sol Ctrl | Çömelme |

## Networking Modeli

### Transport & Lobby (`SteamNetworkManager`)
- Steam P2P, Fizzy transport üzerinden; ayrı dedicated server yok, host'un bilgisayarı sunucudur
- Transport runtime'da seçilir: `SetTransport`, `StartHost`/`StartClient`'tan önce hem `NetworkManager.transport`'u hem de Mirror'ın statik `Transport.active` değerini ayarlar
- Host Steam lobby oluşturur (`CreateLobbyAsync`), friends-only/joinable işaretler ve lobby ID'sini panoya kopyalar
- Host'un SteamID'si lobby metadata'sına yazılır (`HostSteamID`); istemciler bunu okuyup `StartClient`'ı o ID ile çağırır — manuel IP gerekmez
- `OnP2PSessionRequest` → `AcceptP2PSessionWithUser` P2P handshake'i (NAT delinmesi) tamamlar

### Spawn & Ownership
- Oyuncular: Mirror'ın varsayılan player prefab'ı, `PlayerRespawnManager` tarafından takip edilir
- AI: yalnızca sunucu tarafında spawn olur (`AISpawner.OnStartServer`), `NetworkServer.Spawn` ile
- Oyuncu mermileri, atış yapanın connection'u ile sahipli olarak spawn olur (`NetworkServer.Spawn(bullet, connectionToClient)`) — kişiye özel geri bildirim için
- Respawn `ReplacePlayerForConnection` ile yapılır

### İstemci → Sunucu (`[Command]`)
- `CmdUpdateTransform` (konum + Y yönü) her frame
- `CmdUpdateAimData` (nişan noktası, nişan durumu, hedef geçerliliği) yalnızca değişince
- `CmdShoot` (spawn konumu + dağılım ayarlı yön), `CmdUpdateAnimationState` (yürüme/koşma/çömelme) — o da değişim kontrolüyle

### Sunucu → İstemci (`[SyncVar]`)
- Oyuncu konumu/dönüşü ve animasyon değerleri
- `Health`/`DummyHealth`: hook'lu `currentHealth` + `isDead` SyncVar'ları — HUD ve ölüm durumu her makinede sunucu verisinden güncellenir
- `isRagdoll` SyncVar hook'u animasyon ↔ ragdoll geçişlerini tetikler
- AI animasyonu sunucuda hesaplanır, istemcilerde yumuşatılır

### Efekt & Geri Bildirim RPC'leri
- `[ClientRpc]` namlu parlaması, mermi gizleme, çarpma parçacıkları, AI respawn, ragdoll darbe kuvveti
- `[TargetRpc]` isabet geri bildirimi (ses + hasar popup'ı) yalnızca atış yapan oyuncuya

### Güven sınırı nerede
- Mermi çarpışması sunucuda çözülür; istemci yalnızca atış isteği gönderir, hasar taklit edilemez
- Hareket owner onaylıdır ve SyncVar ile kopyalanır — co-op shooter için uygun (rollback/tick-sync yok)
- Rekabetçi bir ürüne giderken eklenmesi gerekenler: sabit tick rate, sunucu tarafı ateş hızı limiti ve interpolasyon buffer'ları

## Lisans

Kişisel portfolyo projesi.