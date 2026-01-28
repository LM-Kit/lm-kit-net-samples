# React Class Components to Hooks Migration

## Component Conversion Pattern

### Before (Class Component)
```jsx
class UserProfile extends React.Component {
  constructor(props) {
    super(props);
    this.state = { user: null, loading: true };
  }

  componentDidMount() {
    this.fetchUser();
  }

  componentDidUpdate(prevProps) {
    if (prevProps.userId !== this.props.userId) {
      this.fetchUser();
    }
  }

  componentWillUnmount() {
    this.controller?.abort();
  }

  fetchUser = async () => {
    this.controller = new AbortController();
    this.setState({ loading: true });
    const user = await api.getUser(this.props.userId);
    this.setState({ user, loading: false });
  };

  render() {
    const { user, loading } = this.state;
    if (loading) return <Spinner />;
    return <div>{user.name}</div>;
  }
}
```

### After (Hooks)
```jsx
function UserProfile({ userId }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    
    async function fetchUser() {
      setLoading(true);
      const user = await api.getUser(userId);
      setUser(user);
      setLoading(false);
    }
    
    fetchUser();
    
    return () => controller.abort();
  }, [userId]);

  if (loading) return <Spinner />;
  return <div>{user.name}</div>;
}
```

## Lifecycle Method Mapping

| Class Lifecycle | Hook Equivalent |
|-----------------|-----------------|
| `constructor` | `useState` initial value |
| `componentDidMount` | `useEffect(..., [])` |
| `componentDidUpdate` | `useEffect(..., [deps])` |
| `componentWillUnmount` | `useEffect` cleanup function |
| `shouldComponentUpdate` | `React.memo` |
| `getDerivedStateFromProps` | `useState` + render logic |
| `getSnapshotBeforeUpdate` | No direct equivalent |
| `componentDidCatch` | Error boundary (keep class) |

## State Migration

```jsx
// Class
this.state = { count: 0, name: '' };
this.setState({ count: 1 });
this.setState(prev => ({ count: prev.count + 1 }));

// Hooks
const [count, setCount] = useState(0);
const [name, setName] = useState('');
setCount(1);
setCount(prev => prev + 1);
```

## Ref Migration

```jsx
// Class
this.inputRef = React.createRef();
<input ref={this.inputRef} />
this.inputRef.current.focus();

// Hooks
const inputRef = useRef();
<input ref={inputRef} />
inputRef.current.focus();
```

## Context Migration

```jsx
// Class
static contextType = ThemeContext;
this.context.theme;

// Hooks
const { theme } = useContext(ThemeContext);
```

## Checklist

- [ ] Convert state to useState calls
- [ ] Convert lifecycle to useEffect
- [ ] Convert refs to useRef
- [ ] Convert context to useContext
- [ ] Extract custom hooks for reuse
- [ ] Keep error boundaries as classes
- [ ] Update tests
- [ ] Verify effect dependencies with ESLint
