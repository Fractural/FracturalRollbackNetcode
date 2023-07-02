# Fractural Rollback Netcode 🔃

This is a C# implementation of rollback netcode in Godot. This is based off of [SnopekGames' Godot Rollback Netcode addon](https://gitlab.com/snopek-games/godot-rollback-netcode).

## Usage

### Getting Input

```C#
public class Game : Node2D
{
    public enum Scenes
    {
        Player
    }

    NetcodeManager _netcodeManager;
    DIContainer _diContainer;

    PackedScene _playerPrefab;
    Player _player;

    public void _Ready()
    {
        InitDIContainer();
        StartGame();
    }

    private void InitDIContainer()
    {
        _netcodeManager = new NetcodeManager();

        _diContainer = new DIContainer();
        AddChild(_diContainer);
        _diContainer.Bind<NetcodeManager>().To(_netcodeManager);
    }

    private void StartGame()
    {
        // Spawn automatically registers the instance to the
        // sync manager if it implements any of the INetwork
        // interfaces.
        _player = _netcodeManager.Spawn(this,
            (int)Scenes.Player,
            _playerPrefab);
    }
}

public class Explosion : Node2D, INetworkSpawnable, IInjectable
{
    public class SpawnData
    {
        public Vector2 position;
    }

    private NetworkTimer _despawnTimer;
    private NetcodeManager _netcodeManager;

    public void Construct(DIContainer container)
    {
        _netcodeManager = container.Resolve<NetcodeManager>();
    }

    public void _Ready()
    {
        _despawnTimer = GetNode<NetworkTimer>("ExplosionTimer");
        _despawnTimer.Timeout += OnDespawnTimerTimeout;
    }

    // ----- INetworkSpawnable ----- //
    public void NetworkSpawn(object data)
    {
        var typedData = data as SpawnData;
        GlobalPosition = typedData.position;
        _despawnTimer.Start();
    }

    public void NetworkDespawn() {}
    // ----- INetworkSpawnable ----- //

    public void OnDespawnTimerTimeout()
    {
        _netcodeManager.Despawn(this);
    }
}

public class Bomb : Node2D, INetworkSpawnable, IInjectable
{
    public enum Scenes
    {
        Explosion
    }

    public class SpawnData
    {
        public Vector2 position;
    }

    private NetworkTimer _explosionTimer;
    private NetcodeManager _netcodeManager;

    public void Construct(DIContainer container)
    {
        _netcodeManager = container.Resolve<NetcodeManager>();
    }

    public void _Ready()
    {
        _explosionTimer = GetNode<NetworkTimer>("ExplosionTimer");
        _explosionTimer.Timeout += OnExplosionTimerTimeout;
    }

    // ----- INetworkSpawnable ----- //
    public void NetworkSpawn(object data)
    {
        var typedData = data as SpawnData;
        GlobalPosition = typedData.position;
        _explosionTimer.Start();
    }

    public void NetworkDespawn() { }
    // ----- INetworkSpawnable ----- //

    public void OnExplosionTimerTimeout()
    {
        _netcodeManager.Spawn(this,
            (int)Scenes.Explosion,
            new Explosion.SpawnData() {
                position = GlobalPosition
            });
        _netcodeManager.Despawn(this);
    }
}

public class Player : Node2D, INetworkSerializable, INetworkInput, INetworkProcess, INetworkSpawner
{
    public enum Scenes
    {
        Bomb
    }

    public class Data
    {
        public Vector2 position;
        public Vector2 attackCooldown;
    }

    public class Input
    {
        public bool bombDropped;
        public Vector2 inputDirection;
    }

    public Data data;

    private NetcodeManager _netcodeManager;

    public void Construct(DIContainer container)
    {
        _netcodeManager = container.Resolve<NetcodeManager>();
    }

    // ----- INetworkSpawner ----- //
    public void SpawnwerSpawned(int id, Node scene)
    {
        if (id == (int) Scenes.Bomb)
        {
            scene.Exploded += OnBombExploded;
        }
    }

    public void SpawnwerDespawned(int id, Node scene)
    {
        if (id == (int) Scenes.Bomb)
        {
            scene.Exploded -= OnBombExploded;
        }
    }
    // ----- INetworkSpawner ----- //

    // ----- INetworkProcess ----- //
    private void NetworkProcess(object input)
    {
        if (input is Input typedInput)
        {
            if (typedInput.bombDropped)
                _netcodeManager.Spawn(this,
                    (int)Scenes.Bomb,
                    new Bomb.SpawnData() {
                        position = GlobalPosition
                    });
        }
    }
    // ----- INetworkProcess ----- //

    // ----- INetworkSerializable ----- //
    public object Save()
    {
        return Object.MemberwiseClone(data);
    }

    public void Load(object data)
    {
        data = Object.MemberwiseClone(data) as Data;
    }
    // ----- INetworkSerializable ----- //

    // ----- INetworkInput ----- //
    public object GetLocalInput()
    {
        return new Input()
        {
            inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
            bombDropped = Input.IsActionJustPressed("ui_accept")
        };
    }

    public object PredictLocalInput(object input, int ticksSinceRealInput)
    {
        var input = Object.MemberwiseClone(input);
        if (input.bombDropped)
            input.bombDropped = false;
        return input;
    }
    // ----- INetworkInput ----- //

    private void OnBombExploded()
    {

    }
}
```
