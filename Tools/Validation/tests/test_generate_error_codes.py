"""Unit tests for generate_error_codes.py."""

import sys
import textwrap
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "generate_error_codes.py"


def _setup_repo(tmp: Path, cs_contents: dict[str, str]) -> Path:
    """Set up a minimal repo layout with provided C# files under src/."""
    src = tmp / "src"
    docs = tmp / "docs"
    src.mkdir(parents=True)
    docs.mkdir()
    for rel_path, body in cs_contents.items():
        p = src / rel_path
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(body, encoding="utf-8")
    return tmp


def test_happy_path_single_surfaced_call(tmp_path, monkeypatch):
    cs = textwrap.dedent("""
        using System;
        namespace Foo {
            class Bar {
                void M() {
                    try { } catch (Exception ex) {
                        ModLogger.Surfaced("QM", "Error charging gold", ex);
                    }
                }
            }
        }
    """)
    _setup_repo(tmp_path, {"Features/QM.cs": cs})
    sys.path.insert(0, str(SCRIPT.parent))
    import generate_error_codes as g

    monkeypatch.setattr(g, "REPO_ROOT", tmp_path)
    monkeypatch.setattr(g, "SRC_DIR", tmp_path / "src")
    monkeypatch.setattr(g, "REGISTRY_PATH", tmp_path / "docs" / "error-codes.md")
    monkeypatch.setattr(sys, "argv", ["generate_error_codes.py"])
    assert g.main() == 0
    out = (tmp_path / "docs" / "error-codes.md").read_text(encoding="utf-8")
    assert "## QM" in out
    assert "Error charging gold" in out
    assert "E-QM-" in out  # suffix will vary, just confirm prefix


def test_collision_detection(tmp_path, monkeypatch):
    cs = textwrap.dedent("""
        void M() {
            ModLogger.Surfaced("QM", "alpha summary", null);
            ModLogger.Surfaced("QM", "beta summary", null);
        }
    """)
    _setup_repo(tmp_path, {"a.cs": cs})
    sys.path.insert(0, str(SCRIPT.parent))
    import generate_error_codes as g

    monkeypatch.setattr(g, "REPO_ROOT", tmp_path)
    monkeypatch.setattr(g, "SRC_DIR", tmp_path / "src")
    monkeypatch.setattr(g, "REGISTRY_PATH", tmp_path / "docs" / "error-codes.md")
    monkeypatch.setattr(g, "compute_suffix", lambda s: "0000")
    monkeypatch.setattr(sys, "argv", ["generate_error_codes.py"])
    rc = g.main()
    assert rc == 1  # collision should fail the run


def test_non_literal_args_rejected(tmp_path, monkeypatch):
    cs = textwrap.dedent("""
        void M(string cat) {
            ModLogger.Surfaced(cat, "msg", null);
        }
    """)
    _setup_repo(tmp_path, {"a.cs": cs})
    sys.path.insert(0, str(SCRIPT.parent))
    import generate_error_codes as g

    monkeypatch.setattr(g, "REPO_ROOT", tmp_path)
    monkeypatch.setattr(g, "SRC_DIR", tmp_path / "src")
    monkeypatch.setattr(g, "REGISTRY_PATH", tmp_path / "docs" / "error-codes.md")
    monkeypatch.setattr(sys, "argv", ["generate_error_codes.py"])
    assert g.main() == 1


def test_suffix_is_stable(tmp_path, monkeypatch):
    sys.path.insert(0, str(SCRIPT.parent))
    import generate_error_codes as g

    a = g.compute_suffix("Error charging gold")
    b = g.compute_suffix("Error charging gold")
    assert a == b
    assert len(a) == 4
    assert all(c in "0123456789abcdef" for c in a)


def test_check_mode_flags_drift(tmp_path, monkeypatch):
    cs = 'void M() { ModLogger.Surfaced("QM", "test", null); }'
    _setup_repo(tmp_path, {"a.cs": cs})
    sys.path.insert(0, str(SCRIPT.parent))
    import generate_error_codes as g

    monkeypatch.setattr(g, "REPO_ROOT", tmp_path)
    monkeypatch.setattr(g, "SRC_DIR", tmp_path / "src")
    monkeypatch.setattr(g, "REGISTRY_PATH", tmp_path / "docs" / "error-codes.md")
    monkeypatch.setattr(sys, "argv", ["generate_error_codes.py", "--check"])
    assert g.main() == 1
