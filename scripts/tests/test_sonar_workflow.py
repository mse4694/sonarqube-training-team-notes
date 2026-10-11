"""Execute the actual scanner-begin script with a stub, without network or secrets."""
import os
from pathlib import Path
import re
import subprocess
import tempfile
import textwrap
import unittest

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = (ROOT / '.github/workflows/sonarqube.yml').read_text()
BEGIN = textwrap.dedent(re.search(
    r'      - name: SonarQube begin\n        run: \|\n(.*?)\n      - name: Build',
    WORKFLOW, re.S).group(1))


class ScannerScopeTests(unittest.TestCase):
    def run_begin(self, **overrides):
        with tempfile.TemporaryDirectory() as d:
            stub = Path(d) / 'dotnet'
            stub.write_text('#!/bin/bash\nprintf "%s\\0" "$@" > "$CAPTURE"\n')
            stub.chmod(0o700)
            capture = Path(d) / 'args'
            env = {**os.environ, 'PATH': d + os.pathsep + os.environ['PATH'],
                   'CAPTURE': str(capture), 'SONAR_PROJECT_KEY': 'test',
                   'SONAR_HOST_URL': 'https://sonarcloud.io', 'SONAR_TOKEN': 'fake-test-token',
                   'SONAR_ORGANIZATION': 'test-org', 'SONAR_BRANCH_ANALYSIS': 'false',
                   'SONAR_QUALITYGATE_WAIT': 'true', 'SONAR_PR_NUMBER': '',
                   'SONAR_PR_HEAD': '', 'SONAR_PR_BASE': '', 'GITHUB_REF_NAME': 'main', **overrides}
            run = subprocess.run(['bash', '-euc', BEGIN], env=env, capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            self.assertNotIn('fake-test-token', run.stdout)
            return capture.read_bytes().decode().rstrip('\0').split('\0')

    def test_cloud_main_explicit(self):
        self.assertIn('/d:sonar.branch.name=main', self.run_begin())

    def test_dispatch_solution_is_not_main(self):
        args = self.run_begin(GITHUB_REF_NAME='solution')
        self.assertIn('/d:sonar.branch.name=solution', args)
        self.assertNotIn('/d:sonar.branch.name=main', args)

    def test_pr_scope_does_not_mix_branch_scope(self):
        args = self.run_begin(SONAR_PR_NUMBER='1', SONAR_PR_HEAD='demo/insecure', SONAR_PR_BASE='main')
        self.assertIn('/d:sonar.pullrequest.key=1', args)
        self.assertIn('/d:sonar.pullrequest.branch=demo/insecure', args)
        self.assertIn('/d:sonar.pullrequest.base=main', args)
        self.assertFalse(any(a.startswith('/d:sonar.branch.name=') for a in args))

    def test_community_omits_unsupported_scope(self):
        args = self.run_begin(SONAR_ORGANIZATION='', SONAR_HOST_URL='http://localhost:9000')
        self.assertFalse(any(a.startswith('/d:sonar.branch.name=') for a in args))

    def test_server_branch_analysis_opt_in(self):
        args = self.run_begin(SONAR_ORGANIZATION='', SONAR_BRANCH_ANALYSIS='true', GITHUB_REF_NAME='solution')
        self.assertIn('/d:sonar.branch.name=solution', args)

    def test_branch_is_one_literal_argument(self):
        self.assertIn('/d:sonar.branch.name=feature/space;echo example',
                      self.run_begin(GITHUB_REF_NAME='feature/space;echo example'))


class ConfigurationTests(unittest.TestCase):
    def test_pr_requires_cloud_or_server_branch_support(self):
        script = textwrap.dedent(re.search(
            r'      - id: check.*?        run: \|\n(.*?)\n  build-test-scan:', WORKFLOW, re.S).group(1))
        for organization, host, branch_support, expected in [
            ('', 'https://internal-sonar.example', 'false', 'false'),
            ('test-org', 'https://sonarcloud.io', 'false', 'true'),
            ('', 'https://internal-sonar.example', 'true', 'true'),
        ]:
            with self.subTest(organization=organization, branch_support=branch_support), tempfile.TemporaryDirectory() as d:
                output = Path(d) / 'out'
                env = {**os.environ, 'SONAR_TOKEN': 'fake', 'GITHUB_EVENT_NAME': 'pull_request',
                       'SONAR_ORGANIZATION': organization, 'SONAR_HOST_URL': host,
                       'SONAR_BRANCH_ANALYSIS': branch_support, 'GITHUB_REF_NAME': 'feature/test',
                       'SONAR_DEFAULT_BRANCH': 'main', 'GITHUB_OUTPUT': str(output),
                       'GITHUB_STEP_SUMMARY': str(Path(d) / 'summary')}
                run = subprocess.run(['bash', '-euc', script], env=env, capture_output=True, text=True)
                self.assertEqual(run.returncode, 0, run.stderr)
                self.assertEqual(output.read_text().strip(), 'enabled=' + expected)

    def test_community_dispatch_only_accepts_default_branch(self):
        script = textwrap.dedent(re.search(
            r'      - id: check.*?        run: \|\n(.*?)\n  build-test-scan:', WORKFLOW, re.S).group(1))
        for branch, expected in [('main', 'true'), ('solution', 'false')]:
            with self.subTest(branch=branch), tempfile.TemporaryDirectory() as d:
                output = Path(d) / 'out'
                env = {**os.environ, 'SONAR_TOKEN': 'fake', 'GITHUB_EVENT_NAME': 'workflow_dispatch',
                       'SONAR_ORGANIZATION': '', 'SONAR_HOST_URL': 'https://internal-sonar.example',
                       'SONAR_BRANCH_ANALYSIS': 'false', 'GITHUB_REF_NAME': branch,
                       'SONAR_DEFAULT_BRANCH': 'main', 'GITHUB_OUTPUT': str(output),
                       'GITHUB_STEP_SUMMARY': str(Path(d) / 'summary')}
                run = subprocess.run(['bash', '-euc', script], env=env, capture_output=True, text=True)
                self.assertEqual(run.returncode, 0, run.stderr)
                self.assertEqual(output.read_text().strip(), 'enabled=' + expected)


if __name__ == '__main__':
    unittest.main()
