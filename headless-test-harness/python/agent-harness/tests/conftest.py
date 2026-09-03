from __future__ import annotations

import os
from pathlib import Path

import pytest


@pytest.fixture(scope="session")
def project_root() -> Path:
    configured = os.environ.get("SLAYTHESPARE2_111_BETA_ROOT")
    return Path(configured or r"E:\Work\SlaytheSpare2-111-beta").resolve(strict=True)


@pytest.fixture(scope="session")
def harness_root() -> Path:
    return Path(__file__).resolve().parents[3]


@pytest.fixture(scope="session")
def schemas_root(harness_root: Path) -> Path:
    return harness_root / "schemas"


@pytest.fixture(scope="session")
def golden_root(harness_root: Path) -> Path:
    return harness_root / "fixtures" / "golden"
