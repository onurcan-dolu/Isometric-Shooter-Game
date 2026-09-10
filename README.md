# Isometric Shooter

Unity ile Mirror networking ve Steam entegrasyonu kullanılarak geliştirilmiş bir **multiplayer isometric shooter**. Oyuncular, küçük bir arenada döndürülebilir kamerayla ateşli silahlar kullanarak AI düşmanlarıyla savaşır.

## Özellikler

- **Multiplayer** - Steam lobby tabanlı P2P networking (Mirror + FizzyFacepunch)
- **Server-Authoritative** - Tüm oyun mantığı (hasar, spawn, AI) sunucu tarafında çalışır
- **Position Sync** - Sahip (`[Command]`) hareketi gönderir, uzak istemciler `[SyncVar]` üzerinden yumuşatır
- **Döndürülebilir Kamera** - Anchor-aim (çapraz hedef) sistemi: kamera nişan noktasını takip eder, oyuncu/araç ekran merkezinden sınırlı (5–8m) kaya bilir; deterministik, FPS'ten bağımsız
- **Araç Sistemi** - Networked araçlar (F ile bin/çık): sürücü/yolcu koltukları, sunucu tarafı fizik, tekerlek dönüşü ve drift dumanı SyncVar senkronu
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
│   ├── Vehicle.cs                  # Networked araç: koltuklar, sürücü input, tekerlek/drift senkronu
│   ├── VehicleSpawner.cs           # Runtime araç spawn'ı (prefab + NetworkServer.Spawn + RegisterPrefab)
│   └── DamagePopup.cs              # Yüzen hasar sayıları
├── Player/
│   ├── PlayerController.cs         # Oyuncu hareketi (WASD, çömelme, koşu, fare nişanı) + position sync
│   ├── CharacterAnimationController.cs # [SyncVar] ile ağ senkronlu animasyon
│   ├── ShootingController.cs       # Silah nişanı, mermi spawn'ı, namlu efektleri
│   ├── PlayerUIController.cs       # Sadece yerel HUD (can göstergesi)
│   ├── WorldCrosshair.cs           # Yüzeyler üzerine yansıtılan 3D nişangah
│   └── TopDownCinemachineController.cs # Anchor-aim top-down kamera (Q/E dönüş, araç offset'i, deterministik takip)
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
| Özel kamera kontrolcüsü | Anchor-aim top-down kamera (Cinemachine kullanılmaz) |
| PROMETEO Car Controller | Araç fiziği (sunucu tarafı external-input modunda) |
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
| F | Araca binme / araçtan inme |
| Q / E | Kamera döndürme |
| Sol Shift | Koşma |
| Sol Ctrl | Çömelme |
| W/A/S/D + Space | Araçta: gaz / geri / yön / el freni (Space) |

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
- `CmdRequestEnter`/`CmdRequestExit` — araca binme/çıkma istekleri
- `CmdSetCarInput` (W/S/A/D/Space maske bitleri) — araçta sürücü girdisi; sunucu Prometeo'yu external-input modunda sürer

### Sunucu → İstemci (`[SyncVar]`)
- Oyuncu konumu/dönüşü ve animasyon değerleri
- `Health`/`DummyHealth`: hook'lu `currentHealth` + `isDead` SyncVar'ları — HUD ve ölüm durumu her makinede sunucu verisinden güncellenir
- `isRagdoll` SyncVar hook'u animasyon ↔ ragdoll geçişlerini tetikler
- AI animasyonu sunucuda hesaplanır, istemcilerde yumuşatılır
- Araç: kök transform `NetworkTransformReliable` ile (prefab root'a eklenir), `seatIndex`/`vehicle` SyncVar'larıyla koltuk ataması, her tekerlek için ayrı `Quaternion` SyncVar ve drift `bool` SyncVar hook'u

### Efekt & Geri Bildirim RPC'leri
- `[ClientRpc]` namlu parlaması, mermi gizleme, çarpma parçacıkları, AI respawn, ragdoll darbe kuvveti
- `[TargetRpc]` isabet geri bildirimi (ses + hasar popup'ı) yalnızca atış yapan oyuncuya

### Güven sınırı nerede
- Mermi çarpışması sunucuda çözülür; istemci yalnızca atış isteği gönderir, hasar taklit edilemez
- Hareket owner onaylıdır ve SyncVar ile kopyalanır — co-op shooter için uygun (rollback/tick-sync yok)
- Rekabetçi bir ürüne giderken eklenmesi gerekenler: sabit tick rate, sunucu tarafı ateş hızı limiti ve interpolasyon buffer'ları

## Araç Sistemi

- **Biniş modeli:** F tuşu → `Physics.OverlapSphere` ile en yakın aracı bulur → `CmdRequestEnter` → sunucu `SyncList<uint> occupants` üzerinden boş koltuğa atar (0. koltuk = sürücü). Sunucu-onaylı; mesafe ve `EnterRange` doğrulaması sunucuda.
- **Sunucu-authoritative sürüş:** Sürücü araçtayken W/S/A/D/Space maskesini `CmdSetCarInput` ile gönderir; sunucudaki `PrometeoCarController` `useExternalInput = true` modunda sürülür. Client'ta Prometeo devre dışı, tüm Rigidbody'ler kinematik ve WheelCollider'lar kapalıdır — client yalnızca görseli renderlar.
- **Kök senkron:** Araç prefab'inin ROOT'unda `NetworkTransformReliable` bulunur (kurulum: prefab root → Add Component). `Vehicle` bu eksikse console uyarısı basar. Client kamerayla ilgisi: araçtayken kamera araç transform'unu takip eder ve araç özel offset değerleri devreye girer.
- **Tekerlek & drift:** Tekerlek mesh'i döndürmeleri 4 ayrı `Quaternion` SyncVar (deadband'lı), drift dumanı `syncDrifting` hook'u ile client'ta `Play()`/`Stop()`.
- **Güvenlik notu:** `CmdSetCarInput` değişim üzerine gönderilir ve **reliable** kanal kullanır — kaybolan "gaz kesme" paketi arabayı gazda bırakmaz.

## Kamera Sistemi

- **Anchor-aim:** Kamera merkezi = nişan noktası (fare imlecinin ekran ofseti × `maxAimLookDistance`), oyuncu/araç bu merkezden en fazla `maxAnchorOffset` (yaya 5m / araç 8m) kayabilir. Fare sola → hedef sağda görünür; ikisi de ekranda kalır.
- **Araç modu:** Binince hedef araç olur, pivot araç konumuna kilitlenir; araç özel offset (yükseklik/mesafe) ve daha geniş bakış mesafesi (50m) devreye girer.
- **Deterministik takip:** Pozisyon/dönüş her kare hedefe birebir eşitlenir — `Time.deltaTime` tabanlı yumuşatma yok; FPS değişimlerinden etkilenmez (titreme önlenir).
- **Kamera yaşam döngüsü:** Player prefab'inin altındaki kamera, spawn'da `SetParent(null)` ile ayrılır (player dönüşü kamerayı titretmez) ve despawn'da player ile birlikte imha edilir.
- **Kontroller:** Q/E yatay yörünge dönüşü; `Camera.main` yerine camera referansı `PlayerController` tarafından verilir (`SetCamera`).

## Lisans

Kişisel portfolyo projesi.